# Unit Audit: `client/src/source/Engine/Object (files 5)`

- **Unit**: `client/src/source/Engine/Object (files 5)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `2` (`4,231` lines): `ZzzInventory.cpp` (`3,914L`), `ZzzInventory.h` (`317L`)
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| NPC & Self Repair Zen formula | `ZzzInventory.cpp:868-915+`, `ZzzInventory.h:200-201` | Caps `ItemValue` at `400,000,000`; `percent = 1 - Durability / MaxDurability`; `repairGold = floor(3.5 * ItemValue^(3/4) * percent) + 1`; broken (`<= 0` dur): `* 2` for DL pets, `* 1.4` for other items; `SelfRepair`: `* 2.5` (`+150%`); rounds down to nearest `100` (`>= 1000`) or `10` (`>= 100`) | Server `RepairItemAction` & Web `itemValue.ts` | Same |
| Castle Siege tax Zen formatting | `ZzzInventory.cpp:832-866` | Adds `NPCShop->GetTaxRate()` % (`ConvertTaxGold`) or `g_nChaosTaxRate` % (`ConvertChaosTaxGold`) | Web `mixWindow.ts:97` (`withTax`) & Server tax rates | Same |
| Item gold color tier & stat table build | `ZzzInventory.cpp:737-765` | Computes `g_iItemInfo[Level]` stat rows `0..iMaxLevel` and formats gold color tiers (`>= 10,000,000` etc.) | Web `itemWindows.ts:104-109` & `ItemExplanationWindow.cpp` | Same |
