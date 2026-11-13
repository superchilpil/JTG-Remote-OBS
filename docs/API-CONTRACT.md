# Companion API Contract (Initial)

All endpoints are served by the Windows companion on the private LAN. Exact paths and schemas may evolve before the first stable release. Every request except health/discovery must be authenticated after pairing.

## REST

| Method | Path | Purpose |
|---|---|---|
| GET | /api/v1/health | Companion health and version |
| GET | /api/v1/obs/status | OBS connection and output status |
| GET | /api/v1/stream/status | Normalized OBS + YouTube lifecycle |
| POST | /api/v1/stream/start | Start OBS output |
| POST | /api/v1/stream/stop | Stop OBS output only |
| GET | /api/v1/youtube/broadcasts | List/selectable broadcasts |
| POST | /api/v1/youtube/broadcasts/{id}/select | Select active broadcast |
| POST | /api/v1/youtube/broadcasts/{id}/go-live | Explicitly transition to live |
| POST | /api/v1/youtube/broadcasts/{id}/end | Explicitly end YouTube broadcast |
| GET | /api/v1/chat/status | Chat connection state |
| GET | /api/v1/chat/history?limit={n} | Recent in-memory chat history |
| POST | /api/v1/chat/messages | Send a chat message |
| POST | /api/v1/chat/messages/{id}/delete | Delete a message when authorized |
| POST | /api/v1/chat/users/{channelId}/timeout | Timeout/ban a user when authorized |
| GET | /api/v1/devices | List paired devices |
| DELETE | /api/v1/devices/{id} | Revoke a paired device |

Pairing/discovery endpoints are intentionally not finalized here. Pairing must use a short-lived one-time code, not a permanent shared password.

## WebSocket

Authenticated clients connect to /api/v1/events.

Every event uses this envelope:

~~~json
{
  "version": 1,
  "type": "chat.message",
  "sequence": 123,
  "occurredAt": "2026-01-01T12:00:00Z",
  "data": {}
}
~~~

Initial event types:
- obs.connection
- obs.stream_status
- stream.lifecycle
- stream.statistics
- youtube.broadcast
- chat.connection
- chat.message
- chat.event
- chat.message_deleted
- chat.moderation
- companion.warning

Chat events include stable YouTube message IDs so clients can deduplicate replayed events after reconnect.

## Command semantics

- Commands are explicit and idempotent where possible.
- Start/stop commands return the accepted operation and clients then observe authoritative state events.
- stream.stop stops OBS output; it does not implicitly end the YouTube broadcast.
- youtube.go-live and youtube.end require a deliberate user action and confirmation in the client UI.
- The server validates all request payloads and checks device permissions before execution.
- No arbitrary OBS request forwarding or shell/process execution is exposed.