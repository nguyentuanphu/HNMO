# Audit Unit: `server/src/GameLogic/PlugIns/CustomRanks`

- **Status**: Checked (on commit `f9b96fc8`, 2026-11-24)
- **Files**: 8 files read in full (`CustomRankCharacterRecord.cs`, `CustomRankConfiguration.cs`, `CustomRankEngine.cs`, `CustomRanksPlugIn.cs`, `CustomRankStore.cs`, `CustomRankStoreData.cs`, `CustomRankSubject.cs`, `CustomRankTierConfig.cs`).
- **Rule Category**: Custom HNMO feature — Custom ranks/titles & Arena rating tiers.

## Findings

1. **Qualification & selection**: `CustomRankEngine.Qualifies` evaluates level, master level, resets, VIP level, missions claimed, Arena rating, and explicit achievement keys. `TrySelectRank` verifies the player still qualifies for the requested tier before persisting.
2. **Thread-safe persistence**: `CustomRankStore` synchronizes on `_stateStore?.SyncRoot ?? _gate` and persists per-character rows via `HnmoFeatureRowSync<CustomRankCharacterRecord>` (`customRanks.character`). No defects found.
