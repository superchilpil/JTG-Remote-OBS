# System Architecture

## Physical topology

```text
Main Gaming PC                         Dedicated Streaming PC
----------------                       ----------------------
Production OBS                         OBS + existing NDI input
- scenes and transitions                     |
- overlays                                   | OBS WebSocket (loopback only)
- microphone and audio mix                   v
- finished NDI output                 JTG Windows Companion
        |                              - OBS control/status
        +------ NDI over LAN --------> - YouTube OAuth/API
                                       - Live chat stream
                                       - LAN HTTPS/WebSocket API
                                                 |
                                      Private LAN / Wi-Fi
                                                 |
                                      Android Kotlin app
```

The main gaming PC remains the production system. The dedicated streaming PC already has its NDI source configured and is treated as an encoder/broadcast endpoint. The companion must not change OBS scenes, sources, audio, filters, transitions, or NDI configuration.

## Windows companion processes

- **OBS adapter:** Connect to OBS WebSocket v5 on loopback. Query stream status and statistics; issue StartStream and StopStream commands. Reconnect safely if OBS restarts.
- **YouTube service:** OAuth authorization, broadcast discovery/selection, lifecycle state, and live chat.
- **LAN gateway:** Authenticated REST endpoints for commands/configuration and WebSocket events for live status/chat.
- **Pairing/device store:** One-time pairing secret, per-device credentials, revocation, and audit events.
- **OBS dock:** Browser-based dock UI served locally by the companion. Shows connection/stream state and YouTube chat.

## Android app

Native Kotlin app. It discovers the companion on the private LAN, pairs via QR code, and uses the companion API. It does not hold Google OAuth refresh tokens or connect directly to OBS.

Primary destinations:
- Dashboard: OBS connection, stream state, duration, bitrate, FPS, dropped frames, start/stop.
- Live Chat: message feed, author badges, Super Chats/membership events, send message, and permitted moderation actions.
- YouTube: select/manage the current broadcast and open YouTube Studio/Live Control Room for settings not exposed by the API.
- Devices/Settings: paired companion, connection, and notification preferences.

## Stream lifecycle

Keep the states distinct:
1. OBS output stopped.
2. OBS output starting/connecting.
3. OBS output active and sending to YouTube.
4. YouTube broadcast ready to go live.
5. YouTube broadcast live.
6. Stopping/ended/error.

The normal phone workflow is **Start Stream** (starts OBS output), then show broadcast readiness and the **Go Live** confirmation/action. The app must not label an encoder connection as publicly live until YouTube confirms the broadcast state. Stopping OBS output and ending the YouTube broadcast are separate actions and must be clearly presented.

## Network security

- Companion API listens only on a selected private LAN interface; loopback remains available for local dock assets.
- No UPnP, port forwarding, public listener, cloud relay, or cellular fallback.
- Pairing QR contains a short-lived pairing secret and companion address, not a permanent bearer token.
- Exchange the pairing secret for a per-device random credential; store hashed credentials and support revocation.
- Use TLS for LAN API traffic with a companion-issued certificate and explicit Android trust/pinning strategy.
- Validate peer address/interface and authenticate every command and WebSocket session.
- Apply rate limits, command allowlists, and audit logging.
- OBS WebSocket stays bound to loopback, with its own password; never forward it to Android.
- Keep YouTube refresh tokens on Windows and protect them with Windows DPAPI.

## Technology

- Windows companion: C# / .NET 9, Generic Host, ASP.NET Core minimal API, OBS WebSocket v5 client.
- Dock: HTML/CSS/TypeScript (served locally; no remote assets required).
- Android: Kotlin, Jetpack Compose, OkHttp/Ktor client, Android Keystore for companion credential storage.
- Shared contract: OpenAPI for REST plus documented WebSocket event envelopes.

## Non-goals

- No NDI SDK or NDI monitoring.
- No scene/source/audio/production controls.
- No remote desktop or arbitrary PC command execution.
- No cloud account or relay operated by this project.
