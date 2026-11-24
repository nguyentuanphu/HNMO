# Unit: server/src/GameServer/MessageHandler/CashShop

Checked against: Season 6/9 client in-game CashShop script currency IDs.

Scope: `server/src/GameServer/MessageHandler/CashShop` (`8` files, `326` lines).

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `CashShopCoinTypeExtensions.cs:15-30` | Client package script coin mapping: `508 -> WCoinC`, `509 -> WCoinP`, `0 -> GoblinPoints`; `GiftStorageInventoryType = 'G'` (`CashShopStorageListRequestHandlerPlugIn.cs:26`) | Season 6/9 client CashShop script price types | Same |
