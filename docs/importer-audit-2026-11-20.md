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

## Round 2: the open list, done

| Area | Now |
|---|---|
| Zen per map (`IGC_ZenDrop.xml`) | New zen table in the rate tuning: on the listed maps the zen of a kill is Min..Max (x monster level at MultiplyChanceRate) instead of the gained EXP (with EXP x1000, HNMO's zen was x1000 too). S9 has it on (100 x level, 50 %); S6 has it off |
| `IGC_AppointItemDrop.xml` | Extra monster drops at DropRate / Denominator, as their own groups (their S9 items are not in HNMO, so none apply today) |
| Box bags `Item_(g,n,l)` | Box contents (`ItemDefinition.DropItems`) where HNMO has none at that level: 1 box gets contents, 29 keep HNMO's |
| Event, mix and NPC reward bags | Still not imported (Blood Castle / Chaos Castle rewards come from HNMO's mini games) and listed in the preview |
| Devil Square / Chaos Castle / Blood Castle | Real start times from `Data/Events/*.dat` (S6) or the event XML (S9) are written to the events' timetables |
| Levels | `MaxNormalLevel` applied; `MaxMasterLevel` above 200 (S9: 330) is kept out, HNMO's master tree stops at 200 |
| Party EXP | New party EXP table in the rate tuning (`NormalParty2..5ExpBonus`, `SetParty3..5ExpBonus` for different classes) used by the party EXP; empty = HNMO's rule |
| Guild | New "Guild limits" plug-in: `GuildCreateLevel` 150, `MaxGuildMember` 80 |
| PK | `PkClearCost` → PK clear command cost; `PKItemDrop = 0` matches HNMO (no item drop on PK death) |
| Map EXP | `IGC_MapAttribute.xml` ExpBonus → map EXP multiplier ((rate + bonus) / rate) |
| Luck on drops | `ItemLuckDrop` 50 % → luck option add chance (HNMO 25 %) |
| Wings | `Wing2MixMaxRate`, `CapeOfLordMixMaxRate`, `FeatherOfCondorMaxRate`, `Wing3MixMaxRate` → maximum rates of craftings 7, 24, 38, 39 |
| Monster stats | `MonsterList.xml` / `IGC_MonsterList.xml` → level, life, damage, defense, rates, ranges, speeds, respawn where they differ (S6: 287 of 381 monsters HNMO has), with undo |
| Weekday happy hours | Bonus events can run on chosen weekdays |
| Shop undo | Restores luck, option and excellent options |
| Time zones | Bonus events have a time zone setting (default Sydney); the import page asks the old server's time zone and HNMO's, and converts happy hours and event starts (weekday shifted when the day changes) |
| Plug-in settings | Written and read with the plug-ins' own JSON converters |

## Still open

- Event, mix and NPC reward bags (they belong to HNMO's mini game rewards).
- Season 9-only items, maps and monsters (Arca, Acheron, elemental spawns, Muun) are skipped by design.