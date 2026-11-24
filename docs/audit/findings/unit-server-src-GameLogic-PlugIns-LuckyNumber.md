# Audit Unit: `server/src/GameLogic/PlugIns/LuckyNumber`

- **Status**: Checked (on commit `4e6f9c9d`, 2026-11-24)
- **Files**: 11 files read in full (`LuckyNumberConfiguration.cs`, `LuckyNumberDrawOutcome.cs`, `LuckyNumberEngine.cs`, `LuckyNumberOwedReward.cs`, `LuckyNumberPickEntry.cs`, `LuckyNumberPlugIn.cs`, `LuckyNumberRound.cs`, `LuckyNumberStateWriter.cs`, `LuckyNumberStore.cs`, `LuckyNumberStoreData.cs`, `LuckyNumberWinnerEntry.cs`).
- **Rule Category**: Custom HNMO feature — Daily Lucky Number draw (`F5 35` + website).

## Findings

1. **Defect (pick not rolled back if Zen deduction fails)**: In `LuckyNumberPlugIn.PickAsync` (`lines 174-180`), `engine.Pick(...)` persists the pick before `player.TryRemoveMoney(cost)` is called. If `TryRemoveMoney` fails due to a concurrent Zen change, the error is returned without rolling back the stored pick.
2. **Note (no per-account claim lock)**: `LuckyNumberPlugIn.ClaimOwedAsync` (`lines 206-235`) reads `PeekOwed`, delivers items, and calls `RemoveOwed` without an account-level lock; concurrent invocations could double-deliver owed jewels.
3. **Draw & storage**: `LuckyNumberEngine.DrawDueRounds` uses `RandomNumberGenerator.GetInt32`, `LuckyNumberStore` synchronizes on `_store.SyncRoot` and retains 30 recent rounds, and `MissionCoinGranter.GrantAsync` uses an idempotent reference (`lucky:{DateKey}:{Number}:{LoginName}`).
