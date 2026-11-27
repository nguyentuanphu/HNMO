# Unit Audit: `client/web/src/game (files 3)`

- **Unit**: `client/web/src/game (files 3)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `9` (`5,120` lines): `instanceEvents.ts`, `inventory.ts`, `itemGlow.ts`, `itemLooks.ts`, `itemTooltip.ts`, `itemUse.ts`, `itemValue.ts`, `items.ts`, `kalima.ts`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Desktop / Server / Suite Reference | Match |
|---|---|---|---|---|
| Inventory / Vault / Shop / Trade / Mix grid geometry & flags | `inventory.ts:8-37` | `EQUIPMENT_SLOTS = 12`, `BAG = first 12 (8x8)`, `VAULT = 8x15`, `SHOP = 8x15`, `TRADE = 8x4`, `MIX = 8x4`; option bitmasks (`OPTION=1`, `LUCK=2`, `SKILL=4`, `EXCELLENT=8`, `ANCIENT=16`, `HARMONY=32`) | Server `InventoryConstants` & Desktop `NewUIInventoryCtrl` | Same |
| Hotkey potion registration & priority fallback order | `itemUse.ts:31-83` | `REGISTERABLE` & `SINGLE` sets; `Q` defaults `healingPotion`, `W` `manaPotion`, `E` `Antidote`, `R` `shieldPotion` unless overridden by bound healing/mana tag; scans bag right-to-left bottom-up (`findItemReverse` `:89-100`) | Desktop `CMyInventory::CanRegisterItemHotKey` & `GetHotKeyItemTypes` | Same |
| NPC item buy/sell price & repair cost formulas | `itemValue.ts:28-160+` | Computes item buy/sell Zen prices & repair cost multipliers across equipment, wings, potions, bolts/arrows, jewels, CashShop/380/Ancient/Socket overrides | Desktop `ZzzInfomation::ItemValue` & server `ItemPriceCalculator` | Same |
| Item tooltip stat, req, 380, Harmony & Socket calculation | `itemTooltip.ts:49-70+` | `LEVEL_COUNT = 16`, `MAX_VALUE = 65535`, `MAX_SOCKETS = 5`, `SOCKET_EMPTY = 0xfe`, `MAX_SOCKET_OPTION = 50`; computes class requirements, attack/def scaling, set options, and warnings | Desktop `ZzzInfomation` / `ItemTooltipValues` | Same |
| Kalima level tables, Symbol of Kundun count & safezone ban | `kalima.ts:16-85` | `KALIMA_MAPS = [24..29, 36]`, `LOST_MAP = 14:28`, `SYMBOL_OF_KUNDUN = 14:29` (`5` symbols), `KALIMA_TICKET = 13:48`, gates `152..158`; min levels `[40,131,181,231,281,331,350]` vs MG/DL/RF `[20,111,161,211,261,311,350]`, `MAX_LEVEL = 400`; refuses ticket in SafeZone or Kalima | Desktop `GMHellas` & server `Kalima` rules | Same |
| Instance event map constants (IT / DG / IG) | `instanceEvents.ts:26-28,93-95` | `ILLUSION_TEMPLE_MAPS = [45..50]`, `DOPPELGANGER_MAPS = [65..68]`, `IMPERIAL_GUARDIAN_MAPS = [69..72]` | Server `MiniGames` map numbers | Same |
| Glow pass tiers, item looks & item group files | `itemGlow.ts:67-71`, `itemLooks.ts`, `items.ts:8-15` | `+7..+15` shader glow tiers (`+7`, `+9`, `+11`, `+13..+15`); loads `Group00..15` JSON definitions | Client visual rendering / data loader | No game rules |

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- 98f231d4 fix(client): fix buff value time assignment, outlaw warp check, and extended bag space check

Evidence:

- inventory.ts countFreeBagCells: bag 12..75 plus 32 slots per unlocked extension (0..4) - same as server InventoryConstants (RowSize 8, InventoryRows 8, RowsOfOneExtension 4, MaximumNumberOfExtensions 4).
