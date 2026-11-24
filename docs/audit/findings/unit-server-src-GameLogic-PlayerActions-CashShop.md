# Audit Findings: `server/src/GameLogic/PlayerActions/CashShop`

- **Unit**: `server/src/GameLogic/PlayerActions/CashShop` (1 file, 474 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference**: Cross-checked with `server/src/GameLogic/CashShop` and [item-options-380-harmony-cashshop.md](../references/item-options-380-harmony-cashshop.md)

## Files Reviewed (in full)
1. `CashShopActions.cs` (1–474)

## Rules & Behavior Summary
- `OpenAsync` / `Close` / `IsAllowedToUse`: Rejects via `ArenaRules`, requires `CashShopConfiguration`, active `CashShopFeaturePlugIn`, `PlayerState.EnteredWorld`, `IsAlive`, and `IsAtSafezone()`; re-checks `IsOpenAndAllowed` on every subsequent action (`ShowPoints`, `Buy`, `Gift`, `UseStorageItem`, `ShowStorage`).
- `BuyAsync` / `GiftAsync` / `PurchaseAsync`: Runs inside `RunPersistenceExclusiveAsync`; validates package, matching `CoinType`, `PriceSequence`, `IsForSale`, `CashShopProductDelivery.CanDeliver`, `IsGiftable` / `IsGiftingEnabled` / `CanGiftToOwnAccount`, `MaximumStorageItems`, `ICashShopPackageBuyingPlugIn`; applies pending coin grants before balance check; deducts coins, creates `CashShopStorageItem` rows and `CashShopPurchaseReceipt`; rolls back coins, receipt, and storage items cleanly on save failure; triggers `ICashShopPackageBoughtPlugIn`.
- `UseStorageItemAsync`: Loads storage excluding `UsedStorageItems` (in-memory guard until DB save completes), validates `storageIndex` and `PriceSequence`, runs `delivery.DeliverAsync`, deletes storage item, records ID in `UsedStorageItems`, saves, and invokes `delivery.DeliveredAsync`.

## Discrepancies & Observations
1. **Gift storage vs normal storage indexing (`CashShopActions.cs:236-243,294-304`)**: `GetStorageItemsAsync` returns all storage items for the account (both normal and `IsGift == true`), and `UseStorageItemAsync` indexes directly by `storageIndex`. Requires the view/client layer (`ICashShopViewPlugIn.ShowStorageAsync`) to supply the exact index in that combined list (or map tab-local indices back to combined indices).
2. **Time-limited Cash Shop products blocked (`CashShopProductExtensions.cs:26` / `CashShopProductDelivery.cs`)**: `CanDeliver` rejects any product with `Duration > TimeSpan.Zero`, so timed seals, pets, and rings return `PurchaseResult.NotForSale`.
