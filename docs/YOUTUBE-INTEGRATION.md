# YouTube Integration

## Authentication boundary

Google OAuth is performed by the Windows companion. Use the installed-app/desktop OAuth flow with PKCE and a loopback redirect where supported. Request only the scopes needed for selected features. Keep access and refresh tokens on Windows and protect persisted token material with DPAPI. Android and the OBS dock receive only normalized data through the authenticated LAN API.

The user must explicitly authorize the YouTube channel that owns or moderates the broadcast. Never ask the user to paste a Google password or API key into the app.

## Broadcast workflow

1. User selects or creates the intended YouTube broadcast using the YouTube Live Control Room/API workflow.
2. Companion resolves the broadcast and its associated stream/chat identifiers.
3. Phone requests **Start Stream**; companion asks OBS to start its configured output.
4. Companion watches OBS WebSocket output state and YouTube broadcast state independently.
5. When the encoder feed is connected and YouTube reports the broadcast is ready, show a distinct **Ready to go live** state.
6. User presses **Go Live** in the app (unless a later explicit setting enables automatic transition). Companion requests the YouTube transition and waits for authoritative state confirmation.
7. Stop stream and end broadcast remain separate, clearly labeled actions. Do not silently end the public broadcast merely because OBS disconnects.

Do not infer YouTube public-live state from OBS outputActive.

## Live chat

Live chat is a first-class feature, not an embedded GhostChat dependency.

- Use YouTube Live Streaming API liveChatMessages.streamList for low-latency server-streamed events where supported.
- Resume after reconnect using the last nextPageToken.
- Implement bounded reconnect/backoff and handle disabled, ended, missing, and permission-denied chat states.
- Normalize messages into a stable internal event model before broadcasting to the dock and Android.
- Support ordinary text, author/channel identity, profile image URL, published time, verified/owner/moderator/member badges, Super Chat, Super Sticker, membership, gift, poll, and moderation-related system events when returned by the API.
- Preserve message order and deduplicate by YouTube message ID.
- Expose send-message only when authorized. Treat delete/ban/timeout as separate moderator capabilities and show them only when the authenticated channel has permission.
- Do not scrape YouTube's web UI or depend on undocumented Innertube endpoints for the primary implementation.

The Windows OBS dock and Android app subscribe to the same companion chat stream. The companion owns the single upstream YouTube connection and fans events out locally.

## Chat UI requirements

Windows dock:
- Compact, resizable chat feed suitable for keeping open beside OBS.
- Auto-scroll with pause-on-scroll and a return-to-latest affordance.
- Distinguish Super Chats, memberships, gifts, polls, and moderation/system events.
- Message input and send action.
- Optional filters for event types and moderators/members.
- Clear connection/authorization state.

Android:
- Dedicated chat tab with readable message density and profile/badge support.
- Send-message composer.
- Highlight paid/member events.
- New-message indicator when scrolled away from latest.
- Optional notifications for selected high-priority events.

## Quota and operational notes

Prefer streamList over frequent list polling. Treat quota and server-streaming errors as recoverable states, reconnect with the documented continuation token, and surface actionable errors without flooding logs. Keep the API key (if one is configured for public metadata) separate from OAuth credentials; private chat access requires user authorization.
