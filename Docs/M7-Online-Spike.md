# M7 Online Spike

**Status (2026-11-18):** The implementation foundation and pure-C# host/client tests are in place. M7 is **not closed**: live UGS/Relay validation, host-kill recovery, the six-device soak test, and production Cloud Code storage remain outstanding.

## Package and transport decisions

- Resolved `com.unity.netcode.gameobjects` 2.14.0, `com.unity.services.multiplayer` 2.5.0, `com.unity.services.authentication` 3.9.0, and `com.unity.services.cloudsave` 3.4.2.
- `UgsOnlineService` uses anonymous Authentication, Multiplayer Services sessions, Relay, session codes, and quick-match filters for the requested 2–6 player count. The project already has a UGS `cloudProjectId`; service configuration and credentials still need to be verified in a live build.
- `NgoNetTransport` sends named messages with NGO reliable-fragmented delivery. `NetCodec` bounds message, snapshot, string, card-id, and command-group sizes and rejects malformed/trailing data.
- UGS host migration is enabled with `WithHostMigration` and a custom `IMigrationDataHandler`. The handler stores a full serialized `SessionRecord`; the elected host restores the game, reclaims its seat, and waits for other clients to reconnect and re-authenticate their session membership.

## Authority and privacy

- `HostSession` owns the authoritative `MejnunGame`. A command group is applied atomically (full-snapshot rollback on any rejection), checked against the authenticated seat, and followed by a redacted state message for every connected seat.
- `SnapshotSerializer.WriteForSeat` preserves the viewer's hand and public board, canonicalizes all other hands and the draw pile, zeros the setup seed/RNG state, and removes turn history for viewers other than the active seat.
- `ClientSession` sends commands without applying them optimistically, then replaces its game only when a new state snapshot differs. The active seat can locally mirror move/undo/redo/fix-board interactions; the host remains the only source of truth.
- Turn timeout, bot takeover, disconnect wait, save/pause, and reconnect are implemented in the pure-C# session core. The first dealer choice is randomized by the host; the online vote UI is intentionally deferred.

## Persistence and recovery limits

- `SessionRecordCodec` is the versioned, bounded serialization format used by UGS host migration. It includes the full game snapshot and seat/authentication table.
- `CloudSaveSnapshotStore` currently writes through `CloudSaveService.Instance.Data.Player` (the host player's private data). It supports local save/pause and async load, but another player cannot use that private record to resume a session after the host is gone. A shared, server-authoritative Cloud Code module and a menu-level saved-session discovery/rejoin flow are still required for that recovery path.
- Built-in UGS session migration and the `SessionMigrated` bridge have only been validated through deterministic codec/core tests; a real host termination/re-election/reconnect test is still required. Migration uploads are periodic, so very early host loss before the first valid migration upload is not covered.
- Friends/platform invites are not implemented; invite by session code is the only supported join path. Quick-match bot-fill preferences and online hint/deck/felt lobby configuration are also follow-ups.

## Verification

- `Mejnun.Net.Tests`: **9/9 passed**, covering redaction primitives, atomic command groups, live move/undo/redo/revert/Fix-board mirroring, wrong-seat rejection, timeout, AI takeover, reconnect, save/pause/resume, codec roundtrips, and migration-record restoration.
- Focused `FrontEndFlowTests.MainMenuBootsWithConditionalResumeAndBackStack`: **1/1 passed**; it verifies the online button is enabled and the front-end layout is ready.
- Full EditMode: **292/301 passed**. The nine failures are in existing M6 cosmetic catalog/unlock/gallery/progression expectations (7) and alternate-back importer fixture counts (2); the new Net tests pass.
- Full PlayMode: **109/112 passed**. The three failures are felt-view presentation, the existing stalemate XP expectation (20 expected, 40 actual), and HUD root identity; none exercise the online bridge.
- No real UGS session, Relay, reconnect, or host-migration test and no six-device soak has been run.