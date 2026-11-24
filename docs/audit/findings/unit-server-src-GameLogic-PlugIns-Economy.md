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
