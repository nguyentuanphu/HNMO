# Unit: server/src/GameServer/RemoteView/PlayerShop

Checked against: classic vs HNMO extended personal shop currency encoding.

Scope: `server/src/GameServer/RemoteView/PlayerShop` (`7` files, `491` lines).

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `PlayerShopPriceEncoding.cs:24-97` | Extended price encoding: `0xFFF0 + coinType` for account coins, `(Group << 12) | (Number & 0xFFF)` for jewel/item prices; negative guard on `moneyPrice > int.MaxValue` | Classic protocol only carries Zen; `0xFFF0` coin/item bartering wire codec is `HNMO custom` | HNMO custom |
| `ShowShopCurrenciesExtendedPlugIn.cs:28` | `MaximumPriceTypes = (byte.MaxValue - 5) / 2` (`125`) currencies | Extended shop currency list packet ceiling | HNMO custom |
