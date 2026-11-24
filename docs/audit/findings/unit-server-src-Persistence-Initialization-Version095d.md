# Audit Findings: `server/src/Persistence/Initialization/Version095d`

Audited unit: `server/src/Persistence/Initialization/Version095d` (30 files, 4,072 lines).

## Scope

- Root initializers: `DataInitialization.cs`, `GameConfigurationInitializer.cs`, `CharacterClassInitialization.cs`, `GameMapsInitializer.cs`, `NpcInitialization.cs`, `SkillsInitializer.cs`, `ChaosMixes.cs`, `Gates.cs`, `InvasionMobsInitialization.cs`.
- Events: `Events/DevilSquareInitializer.cs`.
- Items (`11` files): `Armors.cs`, `BoxOfLuck.cs`, `Constants.cs`, `EventTicketItems.cs`, `Jewelery.cs`, `Jewels.cs`, `Orbs.cs`, `Pets.cs`, `Scrolls.cs`, `Weapons.cs`, `Wings.cs`.
- Maps (`9` files): `Devias.cs`, `DevilSquare1.cs`..`4.cs`, `Icarus.cs`, `Lorencia.cs`, `Noria.cs`, `Tarkan.cs`.

## Rule Comparison Against Reference Suites

| Area | HNMO `0.95d` (`file:line`) | Suite Role & Value | Result |
| --- | --- | --- | --- |
| Classes & PvP mode | `CharacterClassInitialization.cs:28-36` (DK, DW, Elf, MG; `UseClassicPvp=true`) | Season 0.95d 4-class classic ruleset (no DL/Sum/RF; classic PvP without SD) | Intentional 0.95d version scope |
| Item & option caps | `Items/Constants.cs:15,20` (`MaximumItemLevel=11`, `MaximumOptionLevel=4`); `Armors.cs`, `Weapons.cs` | Season 6/9 `+15` max level (`GameServer.ini` / `Constants.cs` in `VersionSeasonSix`) | Intentional 0.95d `+11` cap (`+4` option lv = `+16`) |
| Chaos Machine mixes | `ChaosMixes.cs:49-100,104-288`: `+10` `50%/2M`, `+11` `45%/4M` (`+25%` luck), `ChaosWeapon`, `FirstWings` (`Fail=DowngradedRandom`), `Dinorant` (`70%/250k`, 3 Uniria + Chaos), `DevilSquareTicket` | Season 6/9 client `mix.bmd` (`diff-client-tables.txt:185`: S6 `101`, S9 `159` recipes) & `ChaosBox.ini` (plus +10/+11 classic base rates) | `same` classic core mix rates & inputs for 0.95d scope; `ChaosMixes.cs:18` notes no Talisman of Luck |
| Devil Square (`1..4`) | `Events/DevilSquareInitializer.cs:48-153` (`15-130`, `131-180`, `181-230`, `231-280`; `Enter=5m`, `Play=20m`, `Exit=3m`, `MaxPlayers=10`, 4 reward tiers + 4 waves at `0/5/12/15m`) | Season 6 suite Devil Square INI (`diff-events-all.txt:33`: `enter=300s`, `play=1200s`, `MaxPlayers=15`, DS1..7) | `same` enter/play durations (`300s`/`1200s`); `differs` `MaxPlayers=10` (S6 `15`) and `0.95d` DS `1..4` scope |
| Event tickets | `Items/EventTicketItems.cs:31-33` (Devil Eye/Key drop tiers `Lv 2, 36, 47, 60`, `Chance=1%` base group; Invitation `14:19`) | Season 6 suite Devil Square drop rates & DropItemGroups | `differs` (1% default group chance vs S6 `Eye=80/10000, Key=80/10000` [0.8%]) |
| Box of Luck & Invasions | `Items/BoxOfLuck.cs:60-95` (`50%` +6 gear/jewel drop + `10,000` zen fallback); `InvasionMobsInitialization.cs:45-205` (`#43`, `#54`, `#53` BoK+2, `#44` Red Dragon `B/S/C`) | Season 6 suite monster list & box drops | `same` invasion mob roles & classic Box of Luck drop structure |
| Wings (`1st`), Pets, Orbs, Scrolls | `Items/Wings.cs:46-129` (Elf/Heaven/Satan `12%` dmg/abs + `CanFly` + `DefaultWing` speed, `L106` TODO `+5 lv/upgrade`); `Pets.cs:36-57` (Angel `-20%/+50HP`, Imp `+30%`, Uniria, Dinorant `-10%/+15%/Fly`); `Jewels.cs` (`JoL 14:16`) | Season 6/9 client `ItemList` & `ItemCalc.lua` (`Wings`: 1st `88%-2%/lv` absorb -> `12%` initial at `+0` matches `88%`!) | `same` initial `12%` absorb/damage on 1st wings & pet multipliers; `differs` TODO at `Wings.cs:106` (+5 req/level) |
| Gates & Warps | `Gates.cs:40-57,112-217` (`14` Warps Arena/Lorencia..LostTower7 + Enter/Exit gates for maps `0..7`; `L42` `// todo: update for 0.95d` omits Tarkan/Icarus warps) | Season 6 client `MoveReq_eng.bmd` & `gate.bmd` | `differs` (`0.95d` warps still use 0.75 list without Tarkan/Icarus, noted by `L42` TODO) |
| Skills (`39`) & Buffs | `SkillsInitializer.cs:42-162` (`39` skills + Bali `#150` + Defense/GreaterDef/GreaterDmg/Heal/Alcohol + TripleShot `ExtraProjectiles`) | Season 6 suite `SkillList.xml` | `same` classic 0.95d skill set |
| Maps (`0..4`, `7..10`) | `Tarkan.cs` (`#57..63` `same`, `217` spawns); `Icarus.cs:51,129-391` (`CanFly` req; `#69,70,73,76` `same`; `#71,74` regen `3s` vs S6 `10s`; `#72,75` `3s` vs S6 `30s`; `#77` Phoenix `15s` vs S6 `150s`); `DevilSquare1..4` (`#64..67` `same`) | Season 6 suite `MonsterList.xml` & `MonsterSetBase.txt` | `same` Tarkan/DS combat stats; `differs` Icarus monster/boss respawn timers (`3s/15s` vs S6 `10s/30s/150s`) |
