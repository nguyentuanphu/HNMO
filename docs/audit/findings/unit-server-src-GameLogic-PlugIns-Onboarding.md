# Audit Unit: `server/src/GameLogic/PlugIns/Onboarding`

- **Status**: Checked (on commit `b622ffa7`, 2026-11-24)
- **Files**: 17 files read in full (`OnboardingAccountState.cs`, `OnboardingAdvance.cs`, `OnboardingBoardEvent.cs`, `OnboardingCharacterState.cs`, `OnboardingConfiguration.cs`, `OnboardingEngine.cs`, `OnboardingFacts.cs`, `OnboardingOwedReward.cs`, `OnboardingPlugIn.cs`, `OnboardingStateWriter.cs`, `OnboardingStep.cs`, `OnboardingStore.cs`, `OnboardingStoreData.cs`, `OnboardingTarget.cs`, `StarterKitBuffConfiguration.cs`, `StarterKitConfiguration.cs`, `StarterKitItem.cs`).
- **Rule Category**: Custom HNMO feature — New-player guide, starter kit, and queued rewards.

## Findings

1. **Starter kit & anti-farm rules**: `OnboardingEngine.TakeStarterKit` supports `PerAccount` and `MoneyPerAccount = true` (zen/coins once per account even if characters are deleted/recreated). `OnboardingPlugIn.GiveGoodsAsync` rolls back all added items (`TakeBackAsync`) if inventory or Zen capacity is exceeded.
2. **Note (`OnboardingStore` dirty tracking)**: `OnboardingStore.Changed()` sets `_changed = true` without calling `MarkDirty`/`MarkFullScanPending` on `_charSync`, `_accSync`, or `_owedSync`, while `SaveIfChanged()` passes `useDirtySetOnly: true` (`OnboardingStore.cs:92, 105-107`). Verify `HnmoFeatureRowSync` behavior when auditing `server/src/Persistence`.
