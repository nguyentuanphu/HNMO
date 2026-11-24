# Audit Unit: `server/src/GameLogic/PlugIns/GiftCodes`

- **Status**: Checked (on commit `d334cf62`, 2026-11-24)
- **Files**: 16 files read in full (`GiftCodeDefinition.cs`, `GiftCodeEngine.cs`, `GiftCodeIdentity.cs`, `GiftCodeItem.cs`, `GiftCodeLogEntry.cs`, `GiftCodeOutcome.cs`, `GiftCodeOwedReward.cs`, `GiftCodePendingCoins.cs`, `GiftCodeRedeemer.cs`, `GiftCodeReward.cs`, `GiftCodeRewards.cs`, `GiftCodesConfiguration.cs`, `GiftCodesPlugIn.cs`, `GiftCodeStateWriter.cs`, `GiftCodeStore.cs`, `GiftCodeStoreData.cs`).
- **Rule Category**: Custom HNMO feature — Single & batch gift codes, rate limiting, salted network/device limits, and atomic rewards.

## Findings

1. **Validation & security**: `GiftCodeEngine.Redeem` enforces wrong-try rate limits, Sydney-time windows, `MaxUses`, per-account/character limits, salted SHA-256 network (`OncePerNetwork`) and device (`OncePerDevice`) claims, and level/reset/class restrictions. `BatchCsv` prefixes `=+-@` to prevent CSV formula injection.
2. **Idempotent coins & atomic goods**: `GiftCodeRewards.GrantCoinsAsync` deduplicates via `CashShopCoinGrant.Reference`, and `GiveGoodsAsync` rolls back all added items if bag or Zen capacity is exceeded.
3. **Note**: `GiftCodeStore.Changed()` sets `_changed = true` without calling `MarkDirty`/`MarkFullScanPending` on `_claimsSync`/`_owedSync`/`_wrongTriesSync` while `SaveIfChanged()` passes `useDirtySetOnly: true` (to verify in `Persistence`).
