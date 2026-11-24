# Audit Unit: `server/src/GameLogic/PlugIns/TopUpMilestones`

- **Status**: Checked (on commit `e7f2f26e`, 2026-11-24)
- **Files**: 13 files read in full (`TopUpAccountState.cs`, `TopUpClaimOutcome.cs`, `TopUpCreditOutcome.cs`, `TopUpHistoryEntry.cs`, `TopUpMilestoneEngine.cs`, `TopUpMilestoneItem.cs`, `TopUpMilestonesConfiguration.cs`, `TopUpMilestonesPlugIn.cs`, `TopUpMilestoneStateWriter.cs`, `TopUpMilestoneStep.cs`, `TopUpMilestoneStore.cs`, `TopUpMilestoneStoreData.cs`, `TopUpStepProgress.cs`).
- **Rule Category**: Custom HNMO feature — cumulative top-up milestones (VND thresholds awarding WCoinC + bag items).

## Findings

1. **Idempotent credit & atomic owed-item tracking**: `TopUpMilestoneEngine.Credit` deduplicates by `CreditedReferences` (case-insensitive) and trims history to `MaxHistoryEntries`. `Claim` marks `ClaimedStepIds` and enqueues `Owed` items atomically under `store.Lock` + `RunPersistenceExclusiveAsync` before `DeliverOwedAsync`, preventing double-claim and allowing clean retry on `EnteredWorld` when inventory is full.
2. **Thread-safe persistence**: `TopUpMilestoneStore` uses `store.Lock` for all mutations and `SaveIfChanged()` via `HnmoStateStore`. Note that `TopUpMilestoneEngine.DeliverOwed` checks inventory space and rolls back failed `TryAdd` cleanly. No defects found.
