# Unit Audit: `client/src/source/UI/Inventory (files 2)`

- **Unit**: `client/src/source/UI/Inventory (files 2)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `13` (`5,946` lines): `ItemMng.*`, `ItemOptionTooltipModel.h`, `LuckyItemWnd.*`, `MixInventory.*`, `MixUpdates.*`, `MyInventory.*`, `MyShopInventory.*`
- **Status**: `Checked` (reclassified from `"No game rules"` — holds Mix window rate/charm/zen validation, Lucky Item mix rates (`100%/50%`), Personal Shop price/currency confirmation, and equipped Wizard's Ring effect handling)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Lucky Item mix success rates & MixType codes | `LuckyItemWnd.cpp:54-97` | `GetLuckyItemRate`: `eLuckyItemType_Trade` (Ticket -> Lucky item) = `100%` (crafting type `51`), `eLuckyItemType_Refinery` (Item -> Jewel of Extension) = `50%` (crafting type `52`) | Server Lucky Item Chaos Mix configuration (`51` / `52`) | Same |
| Chaos Machine pre-mix & result state gates | `MixInventory.cpp:167-176,245-250+` | Locks grid during `MIX_REQUESTED`, disallows mixing during `MIX_FINISHED`, requests `CrywolfChaosRateBenefit`, validates recipe zen/level/space/charm rules with `MixMgr` | Desktop `MixMgr` / Web `mixWindow.ts` & server `ChaosMachineMixRequestAction` | Same |
| Personal Shop multi-currency & low-Zen confirmation | `MyShopInventory.cpp:38,194,211,232` | Allows selecting Zen or server-allowed Coin/Item currencies; prompts confirmation when Zen price falls below `ItemValue(..., 2)` | Web `personalShop.ts` & server `PlayerShop` | Same |
| Wizard's Ring `+3` visual/pet equip handling | `MyInventory.cpp:1265-1321` | Attaches cash-shop pets (`Demon`, `Guardian`, `Rudolf`, `Panda`, `Unicorn`, `Skeleton`), handles `ITEM_WIZARDS_RING && Level == 3` (`PARTS_LION`), and sends `PetInfoRequest` for Dark Horse (`:278`) | Client equip presentation + Dark Horse server info sync | Same |
