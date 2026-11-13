using System.Net;
using JTG.RemoteOBS.Companion;

var builder = WebApplication.CreateBuilder(args);

// Keep the companion API on loopback until LAN pairing, TLS and device
// authentication have been implemented and reviewed.
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 47831));
builder.Services.AddSingleton<ObsWebSocketClient>();
builder.Services.AddHostedService<ObsConnectionWorker>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/api/v1/health", (ObsWebSocketClient obs) => Results.Ok(new
{
    application = "JTG Remote OBS Companion",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
    status = "ready",
    obs = obs.Snapshot,
    remoteAccess = "disabled-until-pairing-security-is-implemented"
}));

app.MapGet("/api/v1/obs/status", (ObsWebSocketClient obs) => Results.Ok(obs.Snapshot));

app.MapPost("/api/v1/obs/stream/start", async (ObsWebSocketClient obs, CancellationToken ct) =>
    await obs.SetStreamOutputAsync(true, ct)
        ? Results.Accepted()
        : Results.Problem("OBS is not connected.", statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapPost("/api/v1/obs/stream/stop", async (ObsWebSocketClient obs, CancellationToken ct) =>
    await obs.SetStreamOutputAsync(false, ct)
        ? Results.Accepted()
        : Results.Problem("OBS is not connected.", statusCode: StatusCodes.Status503ServiceUnavailable));

app.MapHealthChecks("/health");
app.Run();

sealed class ObsConnectionWorker(ObsWebSocketClient client) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => client.RunAsync(stoppingToken);
}
