# Unit Audit Findings — `server/src/GameLogic/Bots (files 1)` & `(files 3)`

- **Units**: `server/src/GameLogic/Bots (files 1)` (15 files, 5,007 lines) & `server/src/GameLogic/Bots (files 3)` (4 files, 408 lines)
- **Checked on**: 2026-11-24

## Scope Covered

- `(files 1)`: `BotBuffHandler.cs`, `BotBuild.cs`, `BotChaosMachineHandler.cs`, `BotClassNumbers.cs`, `BotConfiguration.cs`, `BotEquipmentHandler.cs`, `BotFeaturePlugIn.cs`, `BotGearPlanner.cs`, `BotGenerator.cs`, `BotJewelHandler.cs`, `BotManager.cs`, `BotMasterHandler.cs`, `BotMiniGameHandler.cs`, `BotMuHelperSettings.cs`, `BotNameGenerator.cs`
- `(files 3)`: `BotWingHandler.cs`, `HuntingSpot.cs`, `HuntingSpotOccupancy.cs`, `PendingPartyInvite.cs`

## Findings

1. **`BotNameGenerator.PickStyle` (`BotNameGenerator.cs:441-442`)**:
   - In `BuildCompoundName`, if a chosen prefix is 7+ letters (e.g. `"Fallen"`=6, `"Shadow"`=6, `"Thunder"`=7) and `MaxNameLength == 10`, `fitting` checks `prefix.Length + s.Length <= 10`; all `CompoundSuffixes` have length >= 3 (`"Bow"`, `"Axe"`), which handles <= 7 cleanly. If a future suffix list lacked short words, `Pick(fitting)` could return null; currently safe with 3-letter suffixes.
2. **`BotManager.RestartBotAsync` & `BotFeaturePlugIn` lifecycle (OK)**:
   - Properly separates per-server `ServerState`, `StartupPhase` interlocked guards, persistence-isolated `BotPlayer` contexts, master evolution relog, and presence rotation (`ActivityByHour`).
3. **`BotEquipmentHandler`, `BotJewelHandler`, `BotChaosMachineHandler`, `BotWingHandler` (OK)**:
   - Safe rollback if equipping a replacement fails (`TryApplySwapAsync`), jewel stock limits per kind, base-stat requirement verification before jewel/chaos upgrades, and direct wing milestone grants without dropping conjured items on the ground.
4. **Custom HNMO subsystem (OK)**:
   - All rules are custom bot simulation behaviors (`BotCapacityPercent`, `BotMuHelperSettings`, `BotMiniGameHandler` keeping bots out of minigames, `HuntingSpotOccupancy` deconflicting solo bots from party clusters).
