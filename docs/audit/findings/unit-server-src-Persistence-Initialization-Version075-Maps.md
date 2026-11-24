# Audit Findings: `server/src/Persistence/Initialization/Version075/Maps`

Audited unit: `server/src/Persistence/Initialization/Version075/Maps` (9 files, 3,419 lines).

## Scope

- `Arena.cs` (81L), `Atlans.cs` (663L), `BaseMapInitializer.cs` (28L), `Devias.cs` (288L), `Dungeon.cs` (783L), `Exile.cs` (40L), `Lorencia.cs` (291L), `LostTower.cs` (947L), `Noria.cs` (297L).

## Rule Comparison Against Reference Suite

| Map (`file:line`) | Monster Stats vs Suite Monster List | Spawns & Special Rules (`file:line`) | Result |
| --- | --- | --- | --- |
| `Lorencia.cs:46-288` (`Map 0`, 8 monsters `#0,1,2,3,4,6,7,14`) | Level/HP/Dmg/Def/AR/BR/MR/AtkR/VR/MS/AS all match Season 6 suite `MonsterList.xml` except `#0 Bull Fighter` (`L91`) & `#2 Budge Dragon` (`L143`): `RespawnDelay=3s` vs S6 `10s` | `14` NPC + `9` area monster spawns (`290` monsters; S6 `MonsterSetBase` map 0 = `334` total) | `same` combat stats; `differs` respawn delay on `#0, #2` (`3s` vs `10s`) and spawn density (`290` vs `334`) |
| `Dungeon.cs:46-780` (`Map 1`, 11 monsters `#5,8..13,15..18` + 3 traps `#100..102`) | `#5,8,9,10,11,12,13,15,16,17` all match; `#18 Gorgon` (`L679`): `RespawnDelay=10s` vs S6 `150s`; traps `#100..102` (`L705-759`): `MoveDelay` unset (`500ms` in S6) and `#100` `VR=4` vs S6 `1` | `316` single spawns vs S6 `MonsterSetBase` map 1 = `313` | `same` on 10 normal mobs; `differs` Gorgon respawn (`10s` vs `150s`) & trap defaults |
| `Devias.cs:46-285` (`Map 2`, 7 monsters `#19..25`) | `#21..25` all match; `#19 Yeti` (`L87`) & `#20 Elite Yeti` (`L117`): `RespawnDelay=3s` vs S6 `10s` | `9` NPC + `9` area spawns (`560` monsters vs S6 `296`) | `same` combat stats; `differs` Yeti respawn (`3s` vs `10s`) & 0.75 area spawn counts |
| `Noria.cs:45-294` (`Map 3`, 8 monsters `#26..33`) | All 8 monsters match Season 6 suite `MonsterList.xml` across every stat & delay | `6` NPC + `8` area spawns (`1,005` monsters vs S6 `412`) | `same` monster stats; `differs` 0.75 area spawn counts (`1,005` vs `412`) |
| `LostTower.cs:46-944` (`Map 4`, 8 monsters `#34..41` + trap `#103`) | All 8 monsters match; `#103 Meteorite` trap (`L921`): `AttackRange=3` vs S6 `0` | `2` NPC + `594` single spawns vs S6 `640` | `same` monster stats; `differs` trap `#103` range & spawn count (`596` vs `640`) |
| `Exile.cs:13-39` (`Map 5`) | No monsters | No spawns (`0` vs S6 `0`), uses default `TerrainVersionPrefix` (`""` inherited; note `Arena.cs` also inherits `Initialization.BaseMapInitializer`) | `same` (`0` spawns) |
| `Arena.cs:13-80` (`Map 6`) | No monsters defined in 0.75 | `3` NPCs (`L44-46`); `L52-62` BattleZone `Soccer` (`Ground (55,141)-(69,180)`, `LeftGoal (61,139)-(63,140)`, `RightGoal (61,181)-(63,182)`, team spawns `(60,156)`/`(60,164)`) | `0.75` Battle Soccer setup (`SeasonSix` adds spots) |
| `Atlans.cs:45-660` (`Map 7`, 8 monsters `#45..52`) | All 8 monsters (`#45..52`) match Season 6 suite `MonsterList.xml` across every stat & delay | `1` NPC + `337` single spawns (`338` vs S6 `387`); `L48` sets `AddUnderwaterMovementPowerUp()` | `same` monster stats & underwater rule; `differs` spawn count (`338` vs `387`) |
| `BaseMapInitializer.cs:12-28` | Helper base | Sets `TerrainVersionPrefix => "075_"` (`L27`) | `No game rules` (terrain prefix helper) |
