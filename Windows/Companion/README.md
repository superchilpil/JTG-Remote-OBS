# Windows Companion

The companion connects to OBS WebSocket on loopback only. It currently exposes a loopback-only bootstrap API on `http://127.0.0.1:47831`.

## OBS setup

1. In OBS, open **Tools → WebSocket Server Settings**.
2. Enable the WebSocket server.
3. Keep the server bound to localhost / loopback. Do not enable remote connections or expose port 4455 to the LAN.
4. Set a strong OBS WebSocket password.
5. Start the companion with the password in the process environment:

   ```powershell
   $env:OBS_WEBSOCKET_HOST = "127.0.0.1"
   $env:OBS_WEBSOCKET_PORT = "4455"
   $env:OBS_WEBSOCKET_PASSWORD = "your-obs-websocket-password"
   dotnet run --project .\Windows\Companion\JTG.RemoteOBS.Companion.csproj
   ```

   Do not commit the password to appsettings, source control, or logs.

## Local endpoints

- `GET http://127.0.0.1:47831/api/v1/health`
- `GET http://127.0.0.1:47831/api/v1/obs/status`
- `POST http://127.0.0.1:47831/api/v1/obs/stream/start`
- `POST http://127.0.0.1:47831/api/v1/obs/stream/stop`

The stream endpoints control OBS encoder output only. They do not mark the YouTube broadcast live or end the YouTube broadcast. The Android app cannot reach these endpoints yet; LAN pairing, TLS and device authentication must be implemented first.
