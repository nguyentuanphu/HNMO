# Audit Findings: `server/src/GameLogic/PlugIns/Chat`

**Status**: Checked (all 14 files read in full, 1,184 lines)
**References**: [`common-server-and-character-rules-s6-s9.md`](../references/common-server-and-character-rules-s6-s9.md), [`ledger-combat-items-events-2026-11-22.md`](ledger-combat-items-events-2026-11-22.md)

## Scope Covered
- `ChatLog.cs` (116 lines), `ChatLogEntry.cs` (21 lines), `ChatLogPlugIn.cs` (59 lines)
- `ChatMute.cs` (46 lines), `ChatSpamGuard.cs` (50 lines), `ChatSpamGuardConfiguration.cs` (47 lines), `ChatSpamGuardPlugIn.cs` (68 lines), `ChatSpamLimits.cs` (20 lines)
- `CrossSeasonFriendsPlugIn.cs` (281 lines), `OnlineCharacters.cs` (47 lines)
- `OfflineWhisper.cs` (18 lines), `OfflineWhisperConfiguration.cs` (34 lines), `OfflineWhisperPlugIn.cs` (170 lines), `OfflineWhisperStore.cs` (193 lines)

## Rule & Design Comparison

| File & Line | Rule / Behavior | Suite / Decision Reference | Verdict |
|---|---|---|---|
| `ChatSpamLimits.cs:19`, `ChatSpamGuardConfiguration.cs:17-46` | Default spam throttle: max `3` identical messages within `10s`, max `8` total lines within `5s` (`Range(0,100)` repeats/lines, `Range(1,3600)` windows; `0` disables a limit). | HNMO custom anti-spam (S6/S9 suites lack sliding-window chat rate/repeat filters). | HNMO custom |
| `ChatSpamGuard.cs:23-49`, `ChatSpamGuardPlugIn.cs:33-61` | Case-insensitive trimmed matching (`Normalize`); only accepted lines enqueue (`_recent`) so blocked users resume after window expiry; GMs (`CharacterStatus.GameMaster`) are exempt (`:54-57`); shows localized `PlayerMessage.ChatSpamWarning`. | Protects public/guild/whisper/post channels without false-locking after cooldown. | Sound |
| `ChatLog.cs:17,62-104`, `ChatLogPlugIn.cs:28-58` | Circular buffer capped at `2000` (`DefaultCapacity`) entries across all game servers in process, plus optional `PersistentSink`; skips `ChatMessageType.Command` in `ChatMessageSentAsync` (commands call `ChatLogPlugIn.Record` explicitly, e.g. `/post` fixed in `#306`). | Admin panel live chat viewer & persistence hook. | Sound |
| `ChatMute.cs:24-45` | Updates `Account.ChatBanUntil = DateTime.UtcNow + duration` on live `OnlineCharacters` session (avoiding DB overwrite on disconnect) or persists to DB when offline, notifying live players with ceiling minutes. | Matches S6/S9 `ChatBanUntil` model while preventing in-memory overwrite race. | Sound |
| `OfflineWhisperConfiguration.cs:19-33`, `OfflineWhisperStore.cs:58-135`, `OfflineWhisperPlugIn.cs:26,86-169` | Queues whispers for offline existing characters (defaults: max `20` per receiver, `7` days TTL, persisted via `HnmoFeatureRowSync` / `../save/offline-whispers.json`). Delivers `3s` after `CharacterSelection -> EnteredWorld` transition prefixed with localized `OfflineWhisperPrefix` (`(offline)`). Rejects nonexistent characters or full queues. | HNMO custom feature (no S6/S9 equivalent; `Take` skips expired entries and checks player hasn't disconnected during the 3s delay). | HNMO custom |
| `CrossSeasonFriendsPlugIn.cs:31-69,73-95,114-279` | Owner decision (2026-11-21): cross-program friends & whispers across Season 6.3 (`0xE0`), Season 9 (`0xE1`), and Arena (`0xE2`). Publishes online roster every `15s`, refreshes elsewhere presence cache every `10s` (friend list every `20s`), polls cross-whispers every `2s` (delivered as `(<Season>) <text>`). Resolves targets by cross-season character name or same-account friend lookup; disallows cross-season trading. | HNMO custom cross-server bridge backed by `IArenaShared`. | HNMO custom |
| `OnlineCharacters.cs:23-46` | Process-wide `OrdinalIgnoreCase` character lookup; `Remove` compares `(Key, Value)` pair before removal so stale disconnect events never evict a newer session. | Safe cross-GS whisper & mute lookup. | Sound |

## Bugs / Open Issues
- None (`#306` already resolved `/post` omission from `ChatLogPlugIn`).
