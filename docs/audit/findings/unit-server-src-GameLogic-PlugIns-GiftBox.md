# Audit Unit: `server/src/GameLogic/PlugIns/GiftBox`

- **Status**: Checked (on commit `9d97109f`, 2026-11-24)
- **Files**: 14 files read in full (`GiftBoxActivity.cs`, `GiftBoxActivityPlugIn.cs`, `GiftBoxAudience.cs`, `GiftBoxCollection.cs`, `GiftBoxCollectionResult.cs`, `GiftBoxDispatch.cs`, `GiftBoxGameActions.cs`, `GiftBoxLetter.cs`, `GiftBoxPacking.cs`, `GiftBoxPackingStatus.cs`, `GiftBoxReceipt.cs`, `GiftBoxRecipient.cs`, `GiftBoxRequest.cs`, `GiftBoxStateWriter.cs`).
- **Rule Category**: Custom HNMO feature — Account-owned Gift Box mailbox with atomic, replay-safe delivery.

## Findings

1. **Idempotent dispatch & proof**: `GiftBoxDispatch.Send` freezes recipient cohorts and rewards in `giftBox.send`/`giftBox.letter` rows with SHA-256 argument fingerprinting on retry.
2. **Transaction-safe collection**: `GiftBoxPacking.PlanAsync` validates item specs and simulates bag placement (including extensions) on a detached scratch inventory without mutating live state. `GiftBoxCollection.CollectAsync` and `GiftBoxGameActions` serialize under `RunPersistenceExclusiveAsync` + per-account semaphore, stage `CashShopCoinGrant` replay proof in the same transaction, and disconnect before releasing the lock on uncertain saves (`Pending`). No defects found.
