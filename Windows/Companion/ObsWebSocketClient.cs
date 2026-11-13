using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace JTG.RemoteOBS.Companion;

public sealed class ObsWebSocketClient(IConfiguration configuration, ILogger<ObsWebSocketClient> logger)
{
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonObject>> _pendingRequests = new();
    private ClientWebSocket? _socket;
    private DateTimeOffset? _connectedAt;
    private string _connectionState = "disconnected";
    private bool _outputActive;
    private bool _outputReconnecting;
    private double _outputDuration;
    private string? _lastError;

    public object Snapshot => new
    {
        connection = _connectionState,
        outputActive = _outputActive,
        outputReconnecting = _outputReconnecting,
        outputDurationSeconds = _outputDuration,
        connectedAt = _connectedAt,
        lastError = _lastError
    };

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var host = configuration["OBS_WEBSOCKET_HOST"] ?? "127.0.0.1";
        var port = configuration.GetValue("OBS_WEBSOCKET_PORT", 4455);
        var password = configuration["OBS_WEBSOCKET_PASSWORD"];

        if (!IsLoopback(host))
        {
            _connectionState = "blocked-non-loopback";
            _lastError = "OBS WebSocket must remain bound to loopback.";
            logger.LogCritical("Refusing non-loopback OBS WebSocket host {Host}", host);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connectionState = "connecting";
                using var socket = new ClientWebSocket();
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                await socket.ConnectAsync(new Uri($"ws://{host}:{port}"), stoppingToken);
                await IdentifyAsync(socket, password, stoppingToken);

                _socket = socket;
                _connectedAt = DateTimeOffset.UtcNow;
                _connectionState = "connected";
                _lastError = null;
                logger.LogInformation("Connected to OBS WebSocket at {Host}:{Port}", host, port);

                using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var receiveTask = ReceiveLoopAsync(socket, connectionCts.Token);
                var statusTask = PollStatusAsync(socket, connectionCts.Token);

                try
                {
                    var completed = await Task.WhenAny(receiveTask, statusTask);
                    await completed;
                }
                finally
                {
                    connectionCts.Cancel();
                    try { await Task.WhenAll(receiveTask, statusTask); }
                    catch (OperationCanceledException) when (connectionCts.IsCancellationRequested) { }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "OBS connection worker stopped.");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                logger.LogWarning(ex, "OBS WebSocket connection failed");
            }
            finally
            {
                _socket = null;
                _connectedAt = null;
                _outputActive = false;
                _outputReconnecting = false;
                _outputDuration = 0;
                _connectionState = "disconnected";
                FailPendingRequests(new WebSocketException("OBS WebSocket connection ended."));
            }

            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    public async Task<bool> SetStreamOutputAsync(bool start, CancellationToken cancellationToken)
    {
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open) return false;

        await RequestAsync(socket, start ? "StartStream" : "StopStream", null, cancellationToken);
        return true;
    }

    private async Task PollStatusAsync(ClientWebSocket socket, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var result = await RequestAsync(socket, "GetStreamStatus", null, ct);
            _outputActive = result?["outputActive"]?.GetValue<bool>() ?? false;
            _outputReconnecting = result?["outputReconnecting"]?.GetValue<bool>() ?? false;
            _outputDuration = result?["outputDuration"]?.GetValue<double>() ?? 0;
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }

    private async Task IdentifyAsync(ClientWebSocket socket, string? password, CancellationToken ct)
    {
        var hello = await ReceiveJsonAsync(socket, ct);
        if (hello["op"]?.GetValue<int>() != 0)
            throw new InvalidOperationException("OBS did not send a Hello message.");

        var rpcVersion = hello["d"]?["rpcVersion"]?.GetValue<int>() ?? 1;
        var identify = new JsonObject
        {
            ["rpcVersion"] = rpcVersion,
            ["eventSubscriptions"] = 64
        };

        var auth = hello["d"]?["authentication"];
        if (auth is not null)
        {
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("OBS WebSocket password is required. Set OBS_WEBSOCKET_PASSWORD.");

            var salt = auth["salt"]?.GetValue<string>() ?? "";
            var challenge = auth["challenge"]?.GetValue<string>() ?? "";
            var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
            var authentication = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
            identify["authentication"] = authentication;
        }

        await SendJsonAsync(socket, new JsonObject { ["op"] = 1, ["d"] = identify }, ct);
        var identified = await ReceiveJsonAsync(socket, ct);
        if (identified["op"]?.GetValue<int>() != 2)
            throw new InvalidOperationException(
                identified["d"]?["message"]?.GetValue<string>() ?? "OBS identification failed.");
    }

    // There is exactly one reader for the lifetime of a connected socket. It routes
    // request responses by requestId and handles unsolicited OBS events separately.
    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var message = await ReceiveJsonAsync(socket, ct);
            var op = message["op"]?.GetValue<int>();
            var data = message["d"] as JsonObject;
            if (data is null) continue;

            if (op == 7)
            {
                var requestId = data["requestId"]?.GetValue<string>();
                if (requestId is not null && _pendingRequests.TryRemove(requestId, out var completion))
                    completion.TrySetResult((JsonObject)data.DeepClone());
            }
            else if (op == 5)
            {
                HandleEvent(data);
            }
        }
    }

    private void HandleEvent(JsonObject eventData)
    {
        var eventType = eventData["eventType"]?.GetValue<string>();
        var eventPayload = eventData["eventData"] as JsonObject;
        if (eventType != "StreamStateChanged" || eventPayload is null) return;

        _outputActive = eventPayload["outputActive"]?.GetValue<bool>() ?? _outputActive;
        _outputReconnecting = eventPayload["outputReconnecting"]?.GetValue<bool>() ?? _outputReconnecting;
    }

    private async Task<JsonObject?> RequestAsync(
        ClientWebSocket socket, string type, JsonObject? data, CancellationToken ct)
    {
        if (socket.State != WebSocketState.Open)
            throw new WebSocketException("OBS WebSocket is not connected.");

        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<JsonObject>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingRequests.TryAdd(id, completion))
            throw new InvalidOperationException("Could not register OBS request.");

        var requestData = new JsonObject
        {
            ["requestType"] = type,
            ["requestId"] = id
        };
        if (data is not null) requestData["requestData"] = data.DeepClone();

        try
        {
            await _sendGate.WaitAsync(ct);
            try
            {
                await SendJsonAsync(socket, new JsonObject { ["op"] = 6, ["d"] = requestData }, ct);
            }
            finally
            {
                _sendGate.Release();
            }

            var response = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            var status = response["requestStatus"];
            if (status?["result"]?.GetValue<bool>() != true)
                throw new InvalidOperationException(
                    status?["comment"]?.GetValue<string>() ?? $"{type} failed.");

            return response["responseData"] as JsonObject;
        }
        finally
        {
            _pendingRequests.TryRemove(id, out _);
        }
    }

    private void FailPendingRequests(Exception exception)
    {
        foreach (var entry in _pendingRequests)
            if (_pendingRequests.TryRemove(entry.Key, out var completion))
                completion.TrySetException(exception);
    }

    private static async Task SendJsonAsync(ClientWebSocket socket, JsonObject payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static async Task<JsonObject> ReceiveJsonAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("OBS closed the WebSocket connection.");
            if (result.MessageType != WebSocketMessageType.Text)
                throw new WebSocketException("OBS sent a non-text WebSocket message.");

            stream.Write(buffer, 0, result.Count);
            if (stream.Length > 1024 * 1024)
                throw new InvalidOperationException("OBS WebSocket message exceeded 1 MB.");
        } while (!result.EndOfMessage);

        return JsonNode.Parse(stream.ToArray()) as JsonObject
            ?? throw new InvalidOperationException("OBS returned an empty or invalid JSON object.");
    }

    private static bool IsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address);
}
