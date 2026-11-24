# Audit Findings: `server/src/Persistence/Initialization/VersionSeasonSix/Maps (files 2)`

Audited unit: `server/src/Persistence/Initialization/VersionSeasonSix/Maps (files 2)` (31 files, 5,710 lines).

## Scope

- `Devias.cs`, `DevilSquare5.cs`..`7.cs`, `Doppelgaenger1.cs`..`4.cs`, `DuelArena.cs`, `Dungeon.cs`, `Elvenland.cs`, `FieldMonsterSpawn.cs`, `FortressOfImperialGuardian1.cs`..`4.cs`, `Icarus.cs`, `IllusionTemple1.cs`..`6.cs`, `Kalima1.cs`..`7.cs`, `KalimaBase.cs`.

## Rule Comparison Against Reference Suite

| Map(s) (`file:line`) | Monster Stats vs Suite `MonsterList.xml` | Spawns & Map Rules | Result |
| --- | --- | --- | --- |
| `Devias.cs:32-124`, `Dungeon.cs:30`, `Icarus.cs:28` (`Maps 2, 1, 10`) | Inherits 0.75/0.95d defs (`MonsterRespawnFromSuitesUpdatePlugIn` syncs timers) | `Devias.MonsterSpawnLayout` (`L32`) replaces 0.75 quarters; `Dungeon` leaves out `{101,107..110}` + adds `#8` spots; `Icarus` leaves out `{103,105,119,132,154}` + adds `#69..75` (`21` spots) matching suites | `same` spawn layout parity |
| `DevilSquare5..7.cs:38-270` (`Map 32` discrim `5..7`) | `#434..440` (`DS7` Gigantis..Dark Elf) all match S6 `MonsterList.xml` | `7` spawn areas/tier; `SafezoneMap = Noria` | `same` |
| `Doppelgaenger1..4.cs:37-53` (`Maps 65..68`) | Event monsters initialized in event/update plug-ins | `SafezoneMap = Elvenland`; `Doppelgaenger3.cs:52` sets `AddUnderwaterMovementPowerUp()` | `same` safezone & underwater terrain rule |
| `DuelArena.cs:37-43` (`Map 64`) | No map monsters | `SafezoneMap = Lorencia` | `same` |
| `Elvenland.cs:207,249-475` (`Map 51`) | All `8` monsters (`#418..425` Strange Rabbit..Captain Grizzly) match S6 `MonsterList.xml` | `16` spawns | `same` |
| `FortressOfImperialGuardian1..4.cs:41-752` (`Maps 69..72`) | `#504..528` define fixed late-game Lv `57..146` stats (e.g. `#504` Gaion `HP=1.8M, Dmg=2680-4130, Def=4550`, `#523` Trap `HP=4500`, `#517` Cavalry `Respawn=10800s`) | S6 `MonsterList.xml:471,479,490` stores level-1 placeholder stats (`Gaion HP=1250 Lv=1`, `Trap HP=100 Lv=2`) scaled dynamically per party level in GS; `SafezoneMap = Devias` | `differs` (HNMO uses static Lv `57..146` stats instead of S6 dynamic party-level scaling) |
| `IllusionTemple1..6.cs:24-71` (`Maps 45..50`) | Uses `#380` Stone Statue & IT monsters | Event stone statue & roaming sorcerer spirit spawns (`IT1..6`), `SafezoneMap = Devias` | `same` |
| `Kalima1..7.cs:117-354`, `KalimaBase.cs:25-35` (`Maps 24..29, 36`) | All `56` monsters (`#144..149,160..161,174..197,260..275,331..338`) match S6 `MonsterList.xml` (incl. `#275 Kundun7` `5,000,000` HP, `2000-2500` dmg, `1500` def, `43200s` respawn at `Kalima7.cs:354-380`) | `61`/`67` spawns per tier, Oracle Layla `#259`, `AddUnderwaterMovementPowerUp()` in `KalimaBase.cs:28` | `same` |
| `FieldMonsterSpawn.cs:15-22` | Record struct helper for shared layout arrays | N/A | `No game rules` |
