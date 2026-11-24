# Unit: server/src/GameLogic/PlayerActions/PlayerStore

Checked against: the Season 6 suite's common settings, server config, and offline-trade config, and the Season 9 suite's common settings.

Scope: `server/src/GameLogic/PlayerActions/PlayerStore` (`5` files, `598` lines).

## Summary

- Core MU personal store rules match the Season 6 and Season 9 suites: minimum character level `6`, Harmony-option items disallowed, non-sellable item flag enforced, zero/negative prices unset or fail, and buyer inventory space + max zen validated under `StoreLock`.
- HNMO adds multi-currency personal store pricing (`ShopPrices` items/coins beyond Zen), view-range verification, escrow/account-safety item lock checks, Arena restriction check, and login store re-opening validation.

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `OpenStoreAction.cs:19,41` | `MinimumLevel = 6` to open personal store | Classic MU / Season 6 & Season 9 Personal Shop unlock level (`6`) | Same |
| `SetItemPriceAction.cs:57,68-78` | `player.Level < 6` rejected; `Amount < 0` clears price & returns `PriceNegative`; `Amount == 0` clears price | Suite Personal Shop level `6` requirement & positive price rule | Same |
| `OpenStoreAction.cs:59` | Rejects opening store if any item has `ItemOptionTypes.HarmonyOption` | Season 6 suite's common settings file (`CanTradeHarmonyItem = 0`); Season 9 suite's common settings file (`CanTradeHarmonyItem = 0`) | Same |
| `BuyRequestAction.cs:76` | Blocks purchase if `item.Definition.IsPersonalStoreSellable == false` | Suite item store restriction flags | Same |
| `BuyRequestAction.cs:36-49,88-93` | Blocks purchase while `BackupInventory != null` (trade/chaos open), outside view range (`SeesTheStoreAsync`), or when `AccountSafetyPlugIn` locks the item | `BackupInventory` & view-range match suite anti-dupe guards; `AccountSafetyPlugIn` is `HNMO custom` | Same + HNMO custom |
| `OpenStoreAction.cs:28,65`, `BuyRequestAction.cs:95-115` | Arena restriction (`ArenaRules.RefuseAsync`), multi-currency pricing (`ShopPrices` coins/items gated by `SupportsShopCurrencies`) | Season 6 off-trade script has optional coin mode (`0=Zen, 1=WCoinC, 2=WCoinP, 3=GoblinPoint`); online multi-currency personal shop & item bartering is `HNMO custom` | HNMO custom |
| `OpenStoreAction.cs:93-135` | Re-opens persisted store on login only if client supports it, otherwise marks closed everywhere | Classic client reconnect behavior | Same |
| `CloseStoreAction.cs`, `StoreItemListRequestAction.cs:37` | Closes store cleanly and returns allowed shop currencies to client | Protocol / UI state | Same / HNMO custom currencies |
