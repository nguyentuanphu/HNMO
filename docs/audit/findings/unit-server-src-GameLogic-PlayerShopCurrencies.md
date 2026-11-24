# Audit Findings — `server/src/GameLogic/PlayerShopCurrencies`

- **Unit**: `server/src/GameLogic/PlayerShopCurrencies` (13 files, 1,015 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `AccountCoinShopCurrencyExchange.cs`, `IShopCurrencyExchange.cs`, `ItemShopCurrencyExchange.cs`
  - `ItemStorePriceExtensions.cs`, `PlayerShopCurrenciesConfiguration.cs`, `PlayerShopCurrenciesFeaturePlugIn.cs`
  - `ShopPaymentResult.cs`, `ShopPrice.cs`, `ShopPriceKind.cs`, `ShopPrices.cs`
  - `ShopPurchase.cs`, `ShowShopCurrenciesAfterEnterWorldPlugIn.cs`, `ZenShopCurrencyExchange.cs`

## Rules & Limits
| Rule | HNMO Implementation | Notes |
| --- | --- | --- |
| Currencies supported | `Zen` (always allowed), `Item` (default `PriceItems` jewels, unbundled or bundles lvl 0–2 = 10/20/30 pcs, never split), `AccountCoin` (`WCoinC`, `WCoinP`, `GoblinPoints` individually toggleable) | `PlayerShopCurrencies` is disabled by default (`IDisabledByDefault`) |
| Price bounds | `DefaultMaximumItemAmount = 1000` (protocol cap `MaximumTransferableItemAmount = 65535`); `DefaultMaximumCoinAmount = 1_000_000`; `Zen` cap `int.MaxValue` | Amounts `<= 0` treated as unpriced (`ItemStorePriceExtensions.cs:23`) |
| Exact jewel subset sum | `ItemShopCurrencyExchange.SelectRows` (`lines 121-157`): DP prefers bundles (`OrderByDescending(Pieces)`), rejects partial/overpay (`ExactAmountNotPayable`), requires untagged tradable items without options/sets | No item creation/destruction; moves existing items inside `TradeContext` |

## Findings & Observations
1. **Two-phase Persistence Atomicity Note — `AccountCoinShopCurrencyExchange.cs:61,77-80`**:
   - Buyer's coin deduction + `CashShopBalanceReceipt` is immediately committed via `purchase.Buyer.SaveProgressAsync()` (`line 61`), whereas seller's coin credit is staged on `purchase.Seller.PersistenceContext` for periodic save (to respect `PlayerPersistence` lock ordering). If the subsequent item move or seller-side step fails after `TryPayAsync` returns `Success`, the buyer's coins are already saved on disk unless explicitly refunded by the caller.
2. **Rollback Bug Risk in `ZenShopCurrencyExchange.cs:28-32`**:
   - If `TryAddMoney` overflows on seller (`line 28`), `purchase.Buyer.TryAddMoney` (`line 30`) restores buyer Zen cleanly.
3. **Suite Comparison**: Extended personal-store multi-currency pricing (`Item` / `WCoin` / `GoblinPoints`) is an HNMO feature beyond base Season 6/9 classic Zen-only personal store.
