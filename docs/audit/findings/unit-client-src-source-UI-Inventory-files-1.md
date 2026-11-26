# Unit Audit: `client/src/source/UI/Inventory (files 1)`

- **Unit**: `client/src/source/UI/Inventory (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `30` (`5,861` lines) including `InventoryActionController.cpp`, `InventoryCtrl.cpp`, `ItemEnduranceInfo.cpp`, `ItemExplanationWindow.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `InventoryActionController.cpp` and `ItemEnduranceInfo.cpp` implement item consumption/ticket/fruit preconditions and durability warning bands)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Consumable right-click dispatch & Elixirs mutex | `InventoryActionController.cpp:666-716` | Handles Town Portal Scroll, Apples/Potions/SD/Elite/Jack/Cherry/Scrolls, and checks Secret Potions (`eBuff_SecretPotion1..5`) mutual exclusion before `SendRequestUse` | Server `ItemConsumeActions` & Secret Potion buff replacement | Same |
| Reset Fruit point floor & DL-only Command fruit check | `InventoryActionController.cpp:718-780` | Subtracts base class starting stats `nStat[MAX_CLASS][5]` (`DW: 18/18/15/30/0`, `DK: 28/20/25/10/0`, `Elf: 22/25/20/15/0`, `MG: 26/26/26/26/0`, `DL: 26/20/20/15/25`, `SUM: 21/21/18/23/0`, `RF: 32/27/25/20/0`); warns via confirmation when `resettable < Durability * 10`; blocks `ITEM_RESET_FRUIT_CONTROL` for non-DL (`OnlyDarklordCanUseIt`) | `CharacterClass` base starting stats & Fruit reset rules (`attributes-and-stats-s6-s9.md`) | Same |
| Chaos Castle Armor of Guardsman preconditions | `InventoryActionController.cpp:782-801` | Refuses entry from Lorencia Market (`IsUnitedMarketPlace()`) and outside Town SafeZone (`!Hero->SafeZone`) | Server `ChaosCastleEnterAction` | Same |
| Blood Castle & Devil Square ticket level calculation | `InventoryActionController.cpp:803-815` | Resolves ticket grade via `CaculateFreeTicketLevel` before `SendMiniGameOpeningStateRequest` | Server `MiniGame` level band calculation | Same |
| Endurance / durability warning thresholds | `InventoryCtrl.cpp:346-350`, `ItemEnduranceInfo.cpp:322-326,421-425,502-507` | Marks durability `<= 0` (broken), `<= 0.2 * max` (critical), `<= 0.3 * max` (warning), `> 0.5 * max` (healthy); Dark Horse / Dark Raven max life `255` | Standard client durability indicator bands | Same |
