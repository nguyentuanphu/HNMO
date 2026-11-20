# Server importer audit: IGCN Season 6 and Season 9 (20 Nov 2026)

Every gameplay config file of both IGCN server suites was checked against what the importer does with it, then both
suites were imported for real into a fresh Season 6 configuration (in memory,
`ServerImporterRealServerTrial`, explicit test).

## Fixed in this branch

| Area | Fault | Now |
|---|---|---|
| Item drops | `ItemDropPer` read but not applied, `ExcItemDropPer` not read | Chances of the common random item group (0.3 → 0.5) and excellent group (0.0001 → 0.002) |
| Item bags | All ~120 `ItemBags/*.xml` became drops of every monster on every map; the merged group took the highest chance (Lucky Coin bag 100 %: every kill drops) | `Monster_(N)` bags are monster N's drops at the bag's `ItemRate`, split between normal and excellent by share; box, event, mix and NPC reward bags (85–90) are listed and not imported |
| Drop groups | Created groups were only in the configuration list, which the drop generator never reads: the imported drops did nothing | Attached to their monster (or map) |
| Ancient drops | `SetItemDropManager` `DropUseRate` read as n/10,000 | n/1,000,000 times the item's weight |
| Spawns | `MonsterSetBase.txt` + `MonsterSetBase_CS.txt` + the copy in `Tools/` all read; added next to HNMO's own spawns (every monster 2–4×) | One list, `Tools` ignored, spawns the map already has are skipped (S6: 3,725, S9: 4,154) |
| Spawns | Event monsters (group 4) and invasion monsters (group 3) became normal spawns; event maps got monsters standing all day | Left to their events (S6: 1,483 + 247 on event maps) |
| Spawns | NPC facing direction lost | Kept |
| Happy hours | Server-wide `IGC_BonusEvent.xml` windows only shown in the timetable; 60 min instead of 2 h; `ItemDropBonus` points read as percent; re-import stacked them | Server-wide bonus events, real duration, +15 points of 50 % = +30 % drops, replaced on re-import |
| Zen | `ZenDropMultipler` applied as a global zen multiplier, but it scales the master-level zen bonus | Shown in the preview only |
| Chaos machine | +10..+15 rates went to recipes 10–15 (wings, pets); then as fixed overrides that dropped the luck bonus | Base rate and luck bonus of upgrade craftings 3, 4, 22, 23, 49, 50 (with undo) |
| Jewels | `UseSoulRate`, `UseSoulWithLuckRate`, `UseLifeRate` ignored | Rate tuning (Soul 50 % + 25 % luck, Life 50 %) |
| Shops | Slots numbered 0, 1, 2 … so large items overlapped; luck/option/excellent lost; GM test shop overwrote Lumen; empty shop file emptied HNMO's store | Placed on the 8×15 grid (0 overlaps in both suites), options kept, GM and empty shops skipped |
| Accounts | Items in inventories and vaults lost luck/option/excellent; 32-byte slots read as 16 | Options kept, slot size detected |
| Version | S6 suite detected as Season 9 because it has Muun files | Season 6 |

## Still open (not done in this branch)

1. Per-map zen table (`IGC_ZenDrop.xml`: S6 disabled, S9 Min/Max 100 per map), `IGC_AppointItemDrop.xml` (54 monster drops), `IGC_MonsterItemDropRate.xml`.
2. Box, event and mix reward bags (Kundun boxes, Blood Castle/Chaos Castle rewards, Lucky Coin): need the item-drop and reward configs, not monster drops.
3. Real Devil Square / Chaos Castle start times from `Data/Events/*.dat` (fallback times are used and only shown).
4. Character limits and rules: max level 400, master level 200/330, party EXP bonuses, guild size and creation level, PK settings, VIP rates, map EXP bonuses (`IGC_MapAttribute.xml`).
5. Monster stats (`MonsterList.xml`), wing/feather/cape chaos rates, item luck drop 50 % (HNMO 25 %).
6. Weekday-only bonus windows are skipped (HNMO's bonus events run daily); none exist in either suite.
7. Undo of a shop restores HNMO's old items without their options (backup does not store options).
