# Audit Findings: `server/src/Persistence/Initialization/Updates/SeasonNine`

Audited unit: `server/src/Persistence/Initialization/Updates/SeasonNine` (10 files, 2,759 lines).

## Scope

- `SeasonNineUpdatePlugInBase.cs`, `SeasonNineJinShopsUpdatePlugIn.cs`, `SeasonNineMapsAndGatesUpdatePlugIn.cs`, `SeasonNineMapsCatalog.cs`, `SeasonNineMonstersCatalog.cs`, `SeasonNineMonstersUpdatePlugIn.cs`, `SeasonNineOnlyItemsCatalog.cs`, `SeasonNineOnlyItemsUpdatePlugIn.cs`, `SeasonNineSharedItemsUpdatePlugIn.cs`, `SeasonNineSocketOptionsUpdatePlugIn.cs`.

## Rule Comparison Against Season 9 Reference Suite

| Area (`file:line`) | HNMO Season 9 Rule | Season 9 Suite Role & Value | Result |
| --- | --- | --- | --- |
| Season gating (`SeasonNineUpdatePlugInBase.cs:43-53`) | Applies only when `HnmoSeasonInstance.IsSeasonNine` is `true`; skipped on Season 6 | Environment-selectable S6 vs S9 world | Isolates S9 data cleanly from S6 saves |
| S9-only monsters (`SeasonNineMonstersUpdatePlugIn.cs:55-123`, `SeasonNineMonstersCatalog.cs:19-160`) | `111` monsters & NPCs (`#580..689`), plus `#412` Dark Elf Trainee Soldier respawn `10s -> 10800s` (`3h`) | Season 9 suite `MonsterList.xml` | `same` (**0 stat diffs** across all `111` specs vs S9 XML) |
| S9 maps, gates & spawns (`SeasonNineMapsAndGatesUpdatePlugIn.cs`, `SeasonNineMapsCatalog.cs:17-680`) | `10` S9 maps (`91` Acheron, `92` Acheron Arca War, `95` Debenter, `96` Debenter Arca War, `97` CC Survival, `98..99` IT League 1..2, `100..101` Uruk Mountain / Arca War, `102` Tormented Square), `32` ExitGates, `22` EnterGates (incl. Dungeon shortcuts `475..481`), `572` Spawns | Season 9 suite terrain, gate & spawn files | `same` map/gate topology (`Acheron/Debenter/Uruk/Event` maps & gates) |
| S9 Jin merchants (`SeasonNineJinShopsUpdatePlugIn.cs:22-86`) | Equips `#583` (`Map 91`) & `#604` (`Map 92`) with 32-item potion/bolt/scroll/Armor of Guardsman shop (omits `13-7`) | Season 9 Acheron NPC shops | `same` |
| S9 socket overrides (`SeasonNineSocketOptionsUpdatePlugIn.cs:26-95`) | Water shield protection Lv5 `0.20f -> 0.30f`; Wind max AG `[25..50] -> [50..130]`; Wind AG rec `[3..15] -> [5..13]` | Season 9 suite socket option settings (`SocketSystem.xml` / S9 socket values) | `same` |
| S9 shared item overrides (`SeasonNineSharedItemsUpdatePlugIn.cs:24-82`) | Cape of Fighter (`12:49`) drop Lv `180 -> 150`; Cape of Lord (`13:30`) req Lv `180 -> 150`; Apple/potions (`14:0..6`) stack `3 -> 255` | Season 9 suite `ItemList.xml` & stack limits | `same` (`150` cape Lv, `255` potion stacks) |
| S9-only items (`SeasonNineOnlyItemsUpdatePlugIn.cs`, `SeasonNineOnlyItemsCatalog.cs`) | `279` unique S9 items (`13` Combination Notes `14:191..203` + Lv400 5-socket weapons, wings `12:262..268`, Muuns/evolving pets `13:229..350`, resolving `13-229/230` Wooparoo duplicate) | Season 9 suite `ItemList.xml` | `same` item catalog entries for implemented S9 items (note S9 skills `618..621` noted in `diff-client-tables.txt:359-362` remain unimplemented) |
