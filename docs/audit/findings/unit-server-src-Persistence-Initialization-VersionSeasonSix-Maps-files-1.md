# Audit Findings: `server/src/Persistence/Initialization/VersionSeasonSix/Maps (files 1)`

Audited unit: `server/src/Persistence/Initialization/VersionSeasonSix/Maps (files 1)` (21 files, 5,990 lines).

## Scope

- `Aida.cs`, `Atlans.cs`, `BalgassRefuge.cs`, `BarracksOfBalgass.cs`, `BloodCastle1.cs`..`BloodCastle8.cs`, `BloodCastleBase.cs`, `ChaosCastle1.cs`..`ChaosCastle7.cs`, `CrywolfFortress.cs`.

## Rule Comparison Against Reference Suite

| Map(s) (`file:line`) | Monster Stats vs Suite `MonsterList.xml` | Spawns & Map Rules | Result |
| --- | --- | --- | --- |
| `Aida.cs:39,322-610` (`Map 33`) | All `10` monsters (`#304..309, #549..552`) match S6 `MonsterList.xml` across every stat & delay | `253` spawns (aligned by `FieldSpawnCountsFromSuitesUpdatePlugIn`) | `same` |
| `Atlans.cs:25-54` (`Map 7`) | Inherits `Version075/Maps/Atlans.cs` (`#45..52` `same`) | Leaves out spot `#297` (`L30`), adds 2 `#51` Great Bahamut spots + Marlon `#229` (`L53`) | `same` (aligned with S6/S9 suites) |
| `BalgassRefuge.cs:39,66` (`Map 42`) | `#412 Dark Elf (Trainee Soldier)` matches S6 `MonsterList.xml` (S9 updates regen `10s -> 10800s`) | `9` spawns | `same` (S6) |
| `BarracksOfBalgass.cs:38,104-168` (`Map 41`) | All `3` monsters (`#409..411`) match S6 `MonsterList.xml` | `29` spawns | `same` |
| `BloodCastleBase.cs`, `BloodCastle1..8.cs:145-306` (`Maps 11..17, 52`) | All `48` monsters (`#84..99, 111..130, 138..143, 428..433`) match S6 `MonsterList.xml` across all stats & delays | `95` spawns per tier (`SafezoneMap = Devias`, `BloodCastleBase.cs:37`) | `same` |
| `ChaosCastle1..7.cs:39-191` (`Maps 18..23, 53`) | `13/14` monsters (`#162..165, 167..173, 426..427`) match S6 `MonsterList.xml`; `#166` (`ChaosCastle3.cs:159`) sets `MinDmg=165, MaxDmg=168` vs S6 `MonsterList.xml:173` (`DamageMin=168 > DamageMax=165` suite inversion) | `99` spawns per tier (`SafezoneMap = Devias`) | `same` (validates min<=max fix on `#166`) |
| `CrywolfFortress.cs:39,324-674` (`Map 34`) | `#310..317` all match; `#204..208` Wolf Statue/Altars match combat stats (`move/attack delays = 0`); `#209 Wolf Altar5` (`L429`) sets `MoveRange=6, AttackRange=1, ViewRange=5, MoveDelay=600, AttackDelay=1600, Regen=10` vs S6 `0` | `188` spawns | `same` on `#310..317, 204..208`; `differs` movement/delay fields on `#209 Altar5` |
