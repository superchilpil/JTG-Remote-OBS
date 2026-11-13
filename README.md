# JTG Remote OBS

A local-network remote control system for a dedicated OBS streaming PC, with a Windows companion/dock and a native Android app.

## Project goals

- Start and stop the OBS stream from an Android phone on the same LAN.
- Manage the YouTube broadcast lifecycle, including a deliberate **Go Live** confirmation.
- View and participate in YouTube Live Chat from the Windows OBS dock and Android app.
- Monitor encoder/stream health without controlling the production setup.
- Keep the existing NDI workflow untouched.

## Machine responsibilities

### Main gaming PC
Owns the production workflow: scenes, transitions, overlays, microphone/audio mixing, and NDI output. This project does not modify or control those functions.

### Dedicated streaming PC
Already receives the finished NDI feed in OBS. JTG Remote OBS controls OBS streaming, YouTube broadcast management, chat, and status only. It does not configure or monitor NDI.

## Applications

- **Windows companion:** .NET 9, OBS WebSocket 5 client, LAN-only authenticated API, YouTube OAuth/API integration, and an OBS custom dock for status and chat.
- **Android:** Native Kotlin application for pairing, remote stream controls, status, YouTube management, and live chat.
- **Shared:** Versioned API contract and protocol documentation.

## Architecture

The Android app connects only to the Windows companion over the private LAN. The companion connects locally to OBS WebSocket and separately connects to Google/YouTube over the internet for authorized YouTube features. OBS WebSocket is not exposed to the LAN or internet.

YouTube chat is consumed by the companion and fanned out to paired clients over the local API. Google OAuth credentials and refresh tokens stay on the Windows companion; they are never sent to Android.

## Security boundaries

- No cloud relay, hosted backend, public inbound endpoint, or port forwarding.
- Pairing uses a one-time QR code and per-device revocable credentials.
- Companion API binds to the LAN/private interface and rejects non-private peers.
- OBS WebSocket remains loopback-only and password protected.
- YouTube OAuth tokens are stored on Windows using DPAPI.
- Internet access is used only for Google/YouTube API and account authorization.

## Initial implementation order

1. Windows companion foundation and OBS WebSocket status/control.
2. LAN API, pairing, and QR provisioning.
3. YouTube OAuth, broadcast selection/state, and live chat.
4. OBS dock UI for stream status and chat.
5. Kotlin Android app and chat/stream dashboard.
6. Installer, APK, CI validation, and release workflows.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/YOUTUBE-INTEGRATION.md](docs/YOUTUBE-INTEGRATION.md), and [docs/API-CONTRACT.md](docs/API-CONTRACT.md).
