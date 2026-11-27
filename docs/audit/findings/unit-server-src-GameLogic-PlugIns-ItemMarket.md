# Audit Findings — `server/src/GameLogic/PlugIns/ItemMarket`

- **Unit**: `server/src/GameLogic/PlugIns/ItemMarket` (12 files, 1,700 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `MarketAuditEntry.cs`, `MarketBuyClaim.cs`, `MarketConfiguration.cs`, `MarketEngine.cs`
  - `MarketItemSnapshot.cs`, `MarketListing.cs`, `MarketPendingDelivery.cs`, `MarketPendingZen.cs`
  - `MarketPlugIn.cs`, `MarketStateWriter.cs`, `MarketStore.cs`, `MarketStoreData.cs`

## Rules & Behavior
- Escrow Item Market (`IDisabledByDefault`, `F6` or NPC `547`):
  - Currencies: `Zen`, `WCoinC`, `GoblinPoints` enabled (`WCoinP` off by default).
  - One-off access fees: seller `1,000 WCoinC` (also grants buyer access), buyer `200 WCoinC`.
  - `FeePercent = 5.0%` (clamped `0..50%`, `Math.Floor`), `ListingZenFee = 10,000`, `MaxListingsPerAccount = 10`, `ExpiryHours = 168` (7 days), `SafeZoneOnly = true`.
  - Uses `HeldItems` DB storage (`StorageId`) to preserve full item properties across listing/purchase/cancel/expiry with atomic `TryClaimForBuy` / `RestoreClaimedListing` / `WithdrawListing` rollback.

## Real Bugs & Observations
1. **Bug — `MarketPlugIn.cs:468-474` (Orphaned `CashShopBalanceReceipt` on failed `PayAccessAsync` save)**:
   - `PayAccessAsync` records `CashShopBalanceReceipt.Record(..., MarketAccess)` before `player.SaveProgressAsync()`. When save fails (`line 469`), `account` coins are refunded (`line 472`), **but the staged receipt is not removed** (unlike `TryPayBuyerAsync` on line 588 which calls `CashShopBalanceReceipt.RemoveAsync`), leaving a phantom debit receipt for the next save.
2. **NPC Override Note — `MarketPlugIn.cs:126`**:
   - When active with default `MarketNpcNumber = 547`, talking to NPC `547` (Julia) opens the Item Market instead of vanilla Loren Market NPC window `0x25`.

## Re-check 2026-11-27 (changes 2af1fa59..a108e846)

Commits read (every changed file read in full):

- 478f9cf3 fix(plugins): fix stack deposits/consumption, receipt rollback, buff id collision, and partypass

Evidence:

- MarketPlugIn access fee: a failed save also removes the coin receipt (it was saved by the next save without the coins taken) - fixed.
