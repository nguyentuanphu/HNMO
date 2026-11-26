# Audit Unit: `server/src/GameLogic/PlugIns/ActivityLog`

- **Status**: Checked (on commit `f9b96fc8`, 2026-11-24)
- **Files**: 6 files read in full (`ActivityKind.cs`, `ActivityLogConfiguration.cs`, `ActivityLogEntry.cs`, `ActivityLogPlugIn.cs`, `ActivityLogQuery.cs`, `ActivityLogStore.cs`).
- **Rule Category**: Custom HNMO feature — Economy & administration activity logging.

## Findings

1. **Row-level persistence & archival**: `ActivityLogStore` persists every `ActivityLogEntry` immediately to `IHnmoPlayerStateRowBackend` (`activityLog.entry`) under `_gate` with `RetentionDays` expiry, and `ActivityLogPlugIn.ExecuteTaskAsync` archives expired rows every 6 hours. Valuable drop/pickup filtering checks Excellent, Ancient, Socket, `Level >= MinValuableDropLevel`, Group 12, and jewel/box/wing ingredients. No defects found.


## Re-check at `8cb8539b` (`2026-11-25`, commits `3d2d358f..8f7a39b7`)
- Added `ActivityItemState` (captures `Guid Serial`, group/number/level/quantity/durability/sockets/skill/sorted options snapshot), retained item history lookup by serial (`b81c766b`), and retained PvP kill history by recorded `ActivityPvpMode` (`8f7a39b7`) plus website tracker privacy/filtering (`a3bb36c8`).
- Gameplay rules and archival behavior unchanged; `ActivityLogStore` query filters properly redact blocked/private accounts.
