using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Safe bootstrap default: loopback only. LAN binding is enabled only after
// pairing, TLS, authentication, and private-interface validation are implemented.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, 47831);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/api/v1/health", () => Results.Ok(new
{
    application = "JTG Remote OBS Companion",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
    status = "starting",
    obs = "not-connected",
    youtube = "not-authorized",
    note = "Bootstrap endpoint only; remote control is not enabled until LAN security is implemented."
}));

app.MapHealthChecks("/health");

app.Run();
