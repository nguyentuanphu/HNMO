# Unit Audit: `client/src/source/GameLogic/Items (files 1)`

- **Unit**: `client/src/source/GameLogic/Items (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `34` (`5,925` lines) including `MixMgr.cpp`, `CComGem.cpp`, `CSItemOption.cpp`, `ItemCategories.cpp`, `ItemLevelVariants.h`, `ItemOptions.cpp`, `PersonalShopCurrency.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Chaos Machine recipes, 380/Harmony/Socket bits & tax | `MixMgr.cpp:33-199,872,1111` | Parses `mix.bmd`, detects `RCP_SP_ADD380ITEM` (`RequireLevel >= 380`), `Harmony`, `Socket`, `Excellent`, `Set` recipe flags, computes recipe success rate & required Zen | Web `mixRecipes.ts` & Server `ChaosMachineMix` | Same |
| Lahap jewel bundle unmix & Zen values | `CComGem.cpp:74,110,250-285` | `CalcCompiledCount = (Level + 1) * 10` (`10/20/30`); bundle item values: Bless `3M*(Lv+1)*10`, Soul `6M`, Life `16M`, Creation/Guardian `15M`, Chaos `810k`, Gem/Harmony/Lower/Higher `18.6k` (`CalcItemValue`) | Server Lahap `JewelMix` & `ItemPriceCalculator` | Same |
| Ancient set option durability & total level reqs | `CSItemOption.cpp:643-826` | Disables Ancient options when `item->Durability <= 0`; accumulates total character/set bonuses | Server `SetItem` activation rules | Same |
| Flight equip & high-value item checks | `ItemCategories.cpp:105-106,467-468`, `ItemLevelVariants.h:15-26` | Flight requires alive wings (`Durability > 0`) or flying mount (`Dinorant`/`Fenrir`, `Durability > 0`); flags high-value sell warning (`Excellent > 0`, non-wing `Level > 6`, Wizard's Ring `+0`) | Web `gates.ts` & `itemUse.ts` | Same |
| Personal shop price packet decoder | `PersonalShopCurrency.cpp:25-58` | Decodes Zen, Coin (`0xFFF0..0xFFF2`), and Item (`Amount > 0`) prices | Web `personalShop.ts:87-93` | Same |
