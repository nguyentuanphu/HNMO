# Audit Unit: `server/src/GameLogic/PlugIns/ActivityLog`

- **Status**: Checked (on commit `f9b96fc8`, 2026-11-24)
- **Files**: 6 files read in full (`ActivityKind.cs`, `ActivityLogConfiguration.cs`, `ActivityLogEntry.cs`, `ActivityLogPlugIn.cs`, `ActivityLogQuery.cs`, `ActivityLogStore.cs`).
- **Rule Category**: Custom HNMO feature — Economy & administration activity logging.

## Findings

1. **Row-level persistence & archival**: `ActivityLogStore` persists every `ActivityLogEntry` immediately to `IHnmoPlayerStateRowBackend` (`activityLog.entry`) under `_gate` with `RetentionDays` expiry, and `ActivityLogPlugIn.ExecuteTaskAsync` archives expired rows every 6 hours. Valuable drop/pickup filtering checks Excellent, Ancient, Socket, `Level >= MinValuableDropLevel`, Group 12, and jewel/box/wing ingredients. No defects found.
