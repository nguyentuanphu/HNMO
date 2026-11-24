# Audit Findings — `server/src/GameLogic/PlugIns/CatchupCap`

- **Unit**: `server/src/GameLogic/PlugIns/CatchupCap` (7 files, 613 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `CatchupCapConfiguration.cs`, `CatchupCapEngine.cs`, `CatchupCapPlugIn.cs`, `CatchupCapSnapshot.cs`
  - `CatchupCapStateWriter.cs`, `CatchupCapStore.cs`, `CatchupCapStoreData.cs`

## Rules & Behavior
- Server-wide progression catch-up cap (`IDisabledByDefault`, Sydney timezone `Australia/Sydney` day boundary at `DayStartHour = 0`):
  - **Level cap** (`LevelCapEnabled = true`): starts at `InitialLevelCap = 300`; once the first player reaches it, increases by `LevelsPerDay = 10` per Sydney day up to `HardMaxLevelCap = 400`.
  - **Daily reset cap** (`ResetCapEnabled = false`): starts at `InitialDailyResets = 3`; once first player hits `TriggerResets = 5`, rises by `DailyResetsPerDay = 1` per Sydney day up to `HardMaxDailyResets = 20`.

## Observations
1. **Delayed Reset Trigger — `CatchupCapPlugIn.cs:128`**:
   - `ObserveResets` runs on `PlayerState.EnteredWorld`, and there is no reset-hook observer inside `CatchupCapPlugIn`. If a player reaches `TriggerResets` without reconnecting (for example in-place reset), the first-player reset trigger waits until their next login unless the reset action triggers `EnteredWorld` (like `MoveToCharacterSelection`).
2. **`CatchupCapEngine.DaysBetween` (`lines 141-150`)**: Uses `(int)(end - start).TotalDays` on `yyyy-MM-dd` local day keys parsed with `DateTimeStyles.None` (no DST fraction drift since midnight dates in `InvariantCulture` subtract cleanly in whole 24h multiples).
