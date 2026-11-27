# Audit Findings — `server/src/GameLogic/PlugIns/Economy`

- **Unit**: `server/src/GameLogic/PlugIns/Economy` (7 files, 595 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `EconomyAccountTotals.cs`, `EconomyAnomaly.cs`, `EconomyConfiguration.cs`, `EconomyDailySnapshot.cs`
  - `EconomyDashboardPlugIn.cs`, `EconomyStore.cs`, `IEconomyExternalAssetProvider.cs`

## Rules & Behavior
- Tracks daily Sydney-date economy snapshots (`RetentionDays = 180`), top holders, activity log drop/mix/NPC sink/faucet totals, and anomaly alerts (`AnomalyJumpPercentThreshold = 50%`, `MinZenDelta = 10M`, `MinJewelDelta = 25`, `MinCoinDelta = 500`).

## Real Bugs Found
1. **Misclassification Bug — `EconomyStore.cs:84-87` (`Jewel of Chaos` counted as 10 jewels)**:
   - `CountJewelPieces` assumes every item with `"Jewel"` in its name and `Group == 12` is a jewel bundle returning `(item.Level + 1) * 10`.
   - However, a single `Jewel of Chaos` is `Group 12, Number 15` (`Level = 0`), so every single `Jewel of Chaos` is counted as `10` jewels (and stacked `Jewel of Chaos` ignores `Durability`).


## Re-check at `8cb8539b` (`2026-11-25`, commit `c65fe142`)
- Added `ConfiguredJewelCounter.cs` (`4f0c742c`) distinguishing configured single jewels vs bundle mixes from world `JewelMix` and `JewelBank` definitions and filtering out bot/template/banned accounts (`IsEligible`).
- **Note**: Legacy `EconomyStore.CountJewelPieces` (`EconomyStore.cs:86-89`) still counts any `Group == 12` `"Jewel"` item as `(Level + 1) * 10` pieces; verify callers migrate to `ConfiguredJewelCounter` (`diffs` logged for Part 9 Item 3).

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- 39443f93 fix(gamelogic): fix anticheat speed/time, season coin race, party master exp gate, chaos jewel count, and gens quest reward

Evidence:

- EconomyStore.CountJewelPieces: group 12 bundles count (level+1)*10, except 12:15 (Jewel of Chaos), which is one piece - fixed.
