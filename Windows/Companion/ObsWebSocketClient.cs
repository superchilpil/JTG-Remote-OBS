using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JTG.RemoteOBS.Companion;

public sealed class ObsWebSocketClient(IConfiguration configuration, ILogger<ObsWebSocketClient> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
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

        if (!IPAddressLoopback(host))
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
                _socket = socket;
                await IdentifyAsync(socket, password, stoppingToken);
                _connectedAt = DateTimeOffset.UtcNow;
                _connectionState = "connected";
                _lastError = null;
                logger.LogInformation("Connected to OBS WebSocket at {Host}:{Port}", host, port);

                while (socket.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
                {
                    var result = await RequestAsync(socket, "GetStreamStatus", null, stoppingToken);
                    _outputActive = result?["outputActive"]?.GetValue<bool>() ?? false;
                    _outputReconnecting = result?["outputReconnecting"]?.GetValue<bool>() ?? false;
                    _outputDuration = result?["outputDuration"]?.GetValue<double>() ?? 0;
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
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
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    public async Task<bool> SetStreamOutputAsync(bool start, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var socket = _socket;
            if (socket is null || socket.State != WebSocketState.Open) return false;
            await RequestAsync(socket, start ? "StartStream" : "StopStream", null, cancellationToken);
            return true;
        }
        finally { _gate.Release(); }
    }

    private async Task IdentifyAsync(ClientWebSocket socket, string? password, CancellationToken ct)
    {
        var hello = await ReceiveJsonAsync(socket, ct);
        if (hello["op"]?.GetValue<int>() != 0) throw new InvalidOperationException("OBS did not send a Hello message.");
        var rpcVersion = hello["d"]?["rpcVersion"]?.GetValue<int>() ?? 1;
        var identify = new JsonObject { ["rpcVersion"] = rpcVersion, ["eventSubscriptions"] = 64 };

        var auth = hello["d"]?["authentication"];
        if (auth is not null)
        {
            if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("OBS WebSocket password is required. Set OBS_WEBSOCKET_PASSWORD.");
            var salt = auth["salt"]?.GetValue<string>() ?? "";
            var challenge = auth["challenge"]?.GetValue<string>() ?? "";
            var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
            var authentication = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
            identify["authentication"] = authentication;
        }

        await SendJsonAsync(socket, new JsonObject { ["op"] = 1, ["d"] = identify }, ct);
        var identified = await ReceiveJsonAsync(socket, ct);
        if (identified["op"]?.GetValue<int>() != 2)
            throw new InvalidOperationException(identified["d"]?["message"]?.GetValue<string>() ?? "OBS identification failed.");
    }

    private async Task<JsonObject?> RequestAsync(ClientWebSocket socket, string type, JsonObject? data, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var requestData = new JsonObject
        {
            ["requestType"] = type,
            ["requestId"] = id
        };
        if (data is not null) requestData["requestData"] = data.DeepClone();
        await SendJsonAsync(socket, new JsonObject { ["op"] = 6, ["d"] = requestData }, ct);

        while (true)
        {
            var message = await ReceiveJsonAsync(socket, ct);
            var op = message["op"]?.GetValue<int>();
            if (op == 7 && message["d"]?["requestId"]?.GetValue<string>() == id)
            {
                var status = message["d"]?["requestStatus"];
                if (status?["result"]?.GetValue<bool>() != true)
                    throw new InvalidOperationException(status?["comment"]?.GetValue<string>() ?? $"{type} failed.");
                return message["d"]?["responseData"] as JsonObject;
            }
            // Output events and other unsolicited messages are intentionally ignored here.
        }
    }

    private static async Task SendJsonAsync(ClientWebSocket socket, JsonObject payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static async Task<JsonNode> ReceiveJsonAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("OBS closed the WebSocket connection.");
            stream.Write(buffer, 0, result.Count);
            if (stream.Length > 1024 * 1024) throw new InvalidOperationException("OBS WebSocket message exceeded 1 MB.");
        } while (!result.EndOfMessage);

        return JsonNode.Parse(stream.ToArray()) ?? throw new InvalidOperationException("OBS returned an empty message.");
    }

    private static bool IPAddressLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address);
}
