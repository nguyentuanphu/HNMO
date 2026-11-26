# Unit Audit: `client/src/source/GameLogic/Items (files 2)`

- **Unit**: `client/src/source/GameLogic/Items (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `11` (`1,409` lines) including `PersonalShopCurrency.h`, `PersonalShopCurrencyName.cpp`, `PersonalShopTitleImp.cpp`, `ShopRestrictions.cpp`, `TradeRestrictions.cpp`, `TransformationRing.cpp`, `ZenAmount.h`
- **Status**: `Checked` (reclassified from `"No game rules"` — holds item restriction rules, Zen caps, currency encoding, and transformation ring mappings)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| NPC sell & personal-shop ban overrides | `ShopRestrictions.cpp:22-70` | `IsSellingBan` / `IsPersonalShopBan` check rented/period items, Heart of Dark Lord (`14:11 +13`), Wizard's Ring (`13:20 != +0`, expired/exp), Rena (`14:21 +1` / Sign of Lord `+3`), Remedy of Love (`+1..+5`), GM Gift, and `ItemAttribute` flags | Server `SellItemToNpcAction`, `PlayerShopSetItemPriceAction` | Same |
| Player-to-player trade & drop/store overrides | `TradeRestrictions.cpp:29-78` | `IsTradeBan` / store/drop bans handle Sign of Lord (`14:21 +3`), Heart of Dark Lord, Wizard's Ring above `+0`, Talisman of Mobility (`Durability == 1`), and item definition flags | Server `TradeItemBlock`, `DropItemAction` | Same |
| Transformation ring model mapping | `TransformationRing.cpp:15-17` | Maps the `6` classic transformation ring levels (`0..5`: Budge Dragon, Giant, Skeleton, Poison Bull, Thunder Lich, Death Cow) | Server transformation ring model IDs | Same |
| Personal shop multi-currency encoding | `PersonalShopCurrency.h:14-45`, `PersonalShopCurrencyName.cpp:13-45` | `PriceItemType`: `0` = Zen, `0xFFF0..0xFFF2` = `WCoinC` / `WCoinP` / `GoblinPoints`, `(group << 12) \| number` (12-bit shift) = Item | `server/src/GameLogic/PlayerShopCurrencies` | Same |
| Inventory & Vault Zen limits | `ZenAmount.h:11-50` | `kMaxInventoryZen = 2,000,000,000`, `kMaxVaultZen = 2,100,000,000`, `MaxVaultDeposit`, `MaxVaultWithdraw` | Server `Vault` & `Inventory` max money caps (inventory 2B, vault capped safely under `int.MaxValue`) | Same |
| Shop title billboard display helper | `PersonalShopTitleImp.cpp` | Billboard UI/PK color formatting only | Presentation only | No game rules |
