# Audit Findings: `server/src/Persistence/Initialization/VersionSeasonSix/Maps (files 3)`

Audited unit: `server/src/Persistence/Initialization/VersionSeasonSix/Maps (files 3)` (17 files, 4,922 lines).

## Scope

- `KanturuEvent.cs`, `KanturuRelics.cs`, `KanturuRuins.cs`, `Karutan1.cs`, `Karutan2.cs`, `LandOfTrials.cs`, `LorenMarket.cs`, `Lorencia.cs`, `LostTower.cs`, `Noria.cs`, `Raklion.cs`, `RaklionBoss.cs`, `SantaVillage.cs`, `SilentMap.cs`, `SwampOfCalmness.cs`, `ValleyOfLoren.cs`, `Vulcanus.cs`.

## Rule Comparison Against Reference Suite

| Map(s) (`file:line`) | Monster Stats vs Suite `MonsterList.xml` | Spawns & Map Rules | Result |
| --- | --- | --- | --- |
| `KanturuEvent.cs:76-333` (`Map 39`) | `#361 Nightmare`, `#362/#363 Maya Hands`, `#364 Maya` all match S6 `MonsterList.xml:329-332` (`HP 1.5M / 400k / 350k / 300`, resists, skills) | `MoonstonePendantEquipped` req (`L134`), `Safezone = KanturuRelics`, `38` Laser Traps (`#106`), `11` wave spawns (`L76-114`) | `same` |
| `KanturuRelics.cs:155-277` (`Map 38`) | `#105..106, #358..360` all match S6 `MonsterList.xml` | `90` spawns | `same` |
| `KanturuRuins.cs:276-628` (`Map 37`) | `#350..357, #553..556` (`12` monsters) all match S6 `MonsterList.xml` | `210` spawns | `same` |
| `Karutan1.cs:188-316`, `Karutan2.cs:45,194-258` (`Maps 80, 81`) | All `8` monsters (`#569..576`) match S6 `MonsterList.xml` | `128` + `133` spawns; `Karutan2` `Safezone = Karutan1` | `same` |
| `LandOfTrials.cs:264-426` (`Map 31`) | `#290..295` (`6` monsters incl. Erohim `#295`) match S6 `MonsterList.xml` | `203` spawns | `same` |
| `Lorencia.cs`, `Noria.cs`, `LostTower.cs`, `LorenMarket.cs`, `SantaVillage.cs`, `SilentMap.cs`, `ValleyOfLoren.cs` | Inherits base monster defs | `Lorencia`/`Noria` add Marlon `#229`, Zyro `#568`, Julia `#547`, Elf Soldier `#257`; `LostTower.cs:30-43` omits `{101..106,216}` and adds `698..700` matching S6/S9 suites; `ValleyOfLoren` spawns Crown/Switches (`216..218`) & Senior/Guardsman (`220,223,224`); `SantaVillage` `Safezone = Devias` | `same` |
| `Raklion.cs:185-444`, `RaklionBoss.cs:54-205` (`Maps 57, 58`) | All `9` field monsters (`#454..458, 562..565`) & `#459..462` Selupan + Eggs match S6 `MonsterList.xml` | `120` + `26` spawns; `RaklionBoss` `Safezone = Raklion` | `same` |
| `SwampOfCalmness.cs:344-699` (`Map 56`) | All `12` monsters (`#441..449, 557..559`) match S6 `MonsterList.xml` | `282` spawns | `same` |
| `Vulcanus.cs:313-665` (`Map 63`) | All `12` monsters (`#480..491`) match S6 `MonsterList.xml` | `246` spawns | `same` |
