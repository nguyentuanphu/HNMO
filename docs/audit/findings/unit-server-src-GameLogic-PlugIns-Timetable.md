# Audit Unit: `server/src/GameLogic/PlugIns/Timetable`

- **Status**: Checked (on commit `e7f2f26e`, 2026-11-24)
- **Files**: 12 files read in full (`FindGmWinnerEntry.cs`, `TimetableComputedEntry.cs`, `TimetableConfiguration.cs`, `TimetableCustomEntry.cs`, `TimetableEngine.cs`, `TimetablePlugIn.cs`, `TimetableSnapshot.cs`, `TimetableSourceCollector.cs`, `TimetableSourceItem.cs`, `TimetableStateWriter.cs`, `TimetableStore.cs`, `TimetableStoreData.cs`).
- **Rule Category**: Custom HNMO feature — Event & Boss Timetable (`F5 33` + website endpoint).

## Findings

1. **Schedule computation & DST handling**: `TimetableEngine.ResolveTiming` converts wall-clock daily times in `Australia/Sydney` (with fallback to `AUS Eastern Standard Time` / UTC) via `ToUtcWallClock`, handling invalid/ambiguous DST transitions. Custom entries cleanly override or extend collected mini-game, invasion, world boss, LMS, and Arena schedules.
2. **Thread-safe winner history**: `TimetableStore` synchronizes reads/writes on `_store.SyncRoot` and caps `FindGmWinners` at `20`. `TimetableStateWriter` escapes non-ASCII characters to `\uXXXX` for safe packet delivery. No critical defects found.

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- 7aae950d Read audited skills, siege timing, rewards and quests from the game

Evidence:

- TimetableSourceCollector: invasion drops come from the configured monsters' drop groups (no more hard-coded lists), Find the GM shows its configured coins/Zen, Castle Siege appears with its real next start / end from the siege schedule when the siege map is active on this server - same as the game data (fixed, PR #392 follow-up). Note: "Castle Siege"/"Công thành chiến" are literals in code like the rest of this plug-in (rule 10 style debt, no player-facing bug).
