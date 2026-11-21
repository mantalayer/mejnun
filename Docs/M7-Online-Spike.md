# M7 Online Spike

**Implementation status:** Disconnect recovery, seat presence and claiming, AI hand-back, reconnect-first joins, the waiting overlay, and host migration recovery are implemented. Live UGS/Relay validation and the six-device soak test remain manual; shared Cloud Code storage and a Paused games menu are out of scope.

## Package and transport decisions

- Resolved `com.unity.netcode.gameobjects` 2.14.0, `com.unity.services.multiplayer` 2.5.0, `com.unity.services.authentication` 3.9.0, and `com.unity.services.cloudsave` 3.4.2.
- `UgsOnlineService` uses anonymous Authentication, Multiplayer Services sessions, Relay, session codes, and quick-match filters for the requested 2–6 player count. The project already has a UGS `cloudProjectId`; service configuration and credentials still need to be verified in a live build.
- `NgoNetTransport` uses the named-message channel `mejnun.session.v2` with NGO reliable-fragmented delivery. `NetCodec` bounds message, snapshot, string, card-id, and command-group sizes and rejects malformed/trailing data. Protocol-v1 clients must update; the channel version makes mixed builds fail explicitly.
- UGS host migration uses `WithHostMigration` with a 5-second SDK upload interval and a custom `IMigrationDataHandler` on sessions created or joined with the corresponding options. The handler stores a full serialized `SessionRecord`, retains the latest non-empty snapshot, and tolerates malformed data. Changed records are also uploaded by the bridge with a 3-second minimum interval and retry backoff. Unity Multiplayer 2.5.0's `ReconnectSessionOptions` does not expose host-migration configuration, so a promoted host reconnecting by session ID explicitly downloads migration data and restarts the Relay network.
- `OnlineReconnectController` sends Away when possible and retries on app resume. During host recovery, clients remain lobby members, refresh the session every 5 seconds and wait up to 180 seconds for promotion; they must not use a code rejoin during that period because leaving can prevent migration. The promoted host reclaims its existing seat, clears the previous host-away pause, and gives other players a 60-second migration grace period. Returning clients resend Join while waiting for Welcome; a stranded host flushes its record before handing off only when other session members remain. Menu joins reconnect by the saved session ID or current joined session before falling back to the code.

## Authority and privacy

- `HostSession` owns the authoritative `MejnunGame` and pure-C# `SeatRoster`. Away/disconnect starts a 20-second grace period and freezes turns, timers, and bots; expiry changes the seat to Disconnected and raises a lobby-removal event. New accounts claim Disconnected/AI-controlled seats before Empty seats; grace seats remain reserved for their owner.
- A command group is applied atomically (full-snapshot rollback on any rejection), checked against the authenticated seat, and followed by a redacted state message for that seat.
- `SnapshotSerializer.WriteForSeat` preserves the viewer's hand and public board, canonicalizes all other hands and the draw pile, zeros the setup seed/RNG state, and removes turn history for viewers other than the active seat.
- `ClientSession` sends commands without applying them optimistically, then replaces its game only when a new state snapshot differs. The active seat can locally mirror move/undo/redo/fix-board interactions; the host remains the only source of truth.
- Turn timeout, bot takeover, disconnect wait, save/pause, and reconnect are implemented in the pure-C# session core. Host choices are Keep waiting or Let AI play. When a player claims an AI-controlled seat during that seat's turn, the AI finishes and control returns at the next turn start.

## Persistence and recovery limits

- `SessionRecordCodec` is the versioned, bounded serialization format used by UGS host migration. It includes the full game snapshot and seat/authentication table.
- `CloudSaveSnapshotStore` currently writes through `CloudSaveService.Instance.Data.Player` (the host player's private data). It supports local save/pause and async load, but another player cannot use that private record to resume a session after the host is gone. A shared, server-authoritative Cloud Code module and a menu-level saved-session discovery/rejoin flow are still required for that recovery path.
- Built-in UGS session migration and the `SessionMigrated` bridge have only been validated through deterministic codec/core tests; a real host termination/re-election/reconnect test is still required. The promoted host retries migration-data downloads and reports a localized failure if no valid record exists; recovery cannot reconstruct a game before the first successful upload.
- A failed host recovery waits up to 180 seconds, then displays a localized failure and Leave action. Mobile suspend can still drop Relay; native background tasks are not included.
- Friends/platform invites are not implemented; invite by session code is the only supported join path. Quick-match bot-fill preferences and online hint/deck/felt lobby configuration are also follow-ups.

## Verification

- `Mejnun.Net.Tests`: **35/35 passed**, including host-promotion seat restoration, malformed-record handling, upload throttling/backoff, and existing session behavior.
- `Mejnun.Localization.Tests`: **71/71 passed**. English entries and references validate; non-English translations for new entries remain expected sync warnings.
- Full EditMode: **408/411 passed**. The three failures are in existing cosmetics expectations: Burgundy is `Level`-locked where `CosmeticContentTests` expects `Free`, plus two dependent `TablePickerTests`. A cosmetics-only run reproduces the same failures.
- No real UGS session, Relay reconnect, host migration, or six-device soak has been run.

## UGS lobby configuration and manual verification

- For fast host takeover, configure the UGS lobby dashboard values to **Disconnect Host Migration Time ≈ 5 seconds** and **Disconnect Removal Time ≈ 60 seconds**. These settings are required for the short recovery experience; the default host-migration delay is about 120 seconds, so the client waits up to 180 seconds to accommodate the default. The application grace period is 20 seconds; the host removes the lobby member after expiry, so the longer dashboard removal timeout does not block seat claims.
- SDK migration uploads are configured at 5-second intervals, with bridge uploads throttled to changed records and a minimum 3-second interval. Reconnected sessions use explicit promotion recovery because `ReconnectSessionOptions` cannot attach the migration handler; clients awaiting promotion must stay in the lobby. Keep the protocol channel at `mejnun.session.v2` for every build that must communicate.
- Manual checklist (not run by the agent):
  1. Host app-switch: observe Away/grace, resume, and confirm the same seat reconnects.
  2. Client app-switch: confirm the host freezes the match, shows the reconnect countdown, and resumes when the client returns.
  3. Kill the host process: confirm host migration, waiting status, and a working new host.
  4. Join by code from another device after a seat disconnects; verify it takes the disconnected seat, not an empty seat.
  5. Let AI play, then claim the seat mid-turn; verify the joiner sees Please wait and gains control at the next turn start.
- Previous PlayMode baseline: **109/112 passed**. The three failures are felt-view presentation, the existing stalemate XP expectation (20 expected, 40 actual), and HUD root identity; PlayMode was not rerun for this reconnect change.