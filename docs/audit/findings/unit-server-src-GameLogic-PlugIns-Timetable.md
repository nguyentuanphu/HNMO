# Audit Unit: `server/src/GameLogic/PlugIns/Timetable`

- **Status**: Checked (on commit `e7f2f26e`, 2026-11-24)
- **Files**: 12 files read in full (`FindGmWinnerEntry.cs`, `TimetableComputedEntry.cs`, `TimetableConfiguration.cs`, `TimetableCustomEntry.cs`, `TimetableEngine.cs`, `TimetablePlugIn.cs`, `TimetableSnapshot.cs`, `TimetableSourceCollector.cs`, `TimetableSourceItem.cs`, `TimetableStateWriter.cs`, `TimetableStore.cs`, `TimetableStoreData.cs`).
- **Rule Category**: Custom HNMO feature — Event & Boss Timetable (`F5 33` + website endpoint).

## Findings

1. **Schedule computation & DST handling**: `TimetableEngine.ResolveTiming` converts wall-clock daily times in `Australia/Sydney` (with fallback to `AUS Eastern Standard Time` / UTC) via `ToUtcWallClock`, handling invalid/ambiguous DST transitions. Custom entries cleanly override or extend collected mini-game, invasion, world boss, LMS, and Arena schedules.
2. **Thread-safe winner history**: `TimetableStore` synchronizes reads/writes on `_store.SyncRoot` and caps `FindGmWinners` at `20`. `TimetableStateWriter` escapes non-ASCII characters to `\uXXXX` for safe packet delivery. No critical defects found.
