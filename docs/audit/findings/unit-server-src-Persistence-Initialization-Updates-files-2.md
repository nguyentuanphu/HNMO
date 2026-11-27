# Audit Findings: `server/src/Persistence/Initialization/Updates (files 2)`

Audited unit: `server/src/Persistence/Initialization/Updates (files 2)` (48 files, 5,724 lines; `AddStardustItemUpdatePlugIn.cs` through `FixCharStatsForceWavePlugInSeason6.cs`).

## Rule Comparison Against Reference Suites

| Update Plug-In (`file:line`) | Applied Rule Value | Suite Role & Value | Result |
| --- | --- | --- | --- |
| `AddSummonerBuffSkillsPlugIn.cs:53-77` | Adds Summoner curse/reflection/sleep attrs, exc reflection `0.05f`, Weakness `10%`, Innovation & Reflection effects | Season 6 skill calc (`RegularSkillCalc.lua:249-343`) | Matches `VersionSeasonSix/Skills` base effects |
| `AdjustIceArrowFreezeDurationUpdatePlugIn.cs:22-55` | Extends `IceArrow` freeze `5s -> 7s` and aligns `IceArrowStr`, `ChainDriveStr`, `StrikeofDestrStr`, `PoisonStr` | Matches `SkillsInitializerBase.cs:176-201` (`7s`) | `same` (`7s` freeze & `10s` cold/poison) |
| `BloodCastleDurationUpdatePlugIn.cs:28-52` | Shortens Blood Castle `GameDuration` `20m -> 15m` | Season 6/9 Blood Castle INI/XML (`diff-events-all.txt:13-14`: `play=900s` = `15m`) | `same` (`15m`) |
| `CrywolfWarpLevelUpdatePlugIn.cs:24-49` | Sets Crywolf warp `#26` `LevelRequirement` `190 -> 300` | Season 6 client `MoveReq_eng.bmd` (`diff-client-tables.txt:202`: Warp #48 Crywolf `Lv=300`) | `same` as S6 (`differs` from `warps.csv` `280` seen in client-tables diff) |
| `DevilSquarePlayerLimitUpdatePlugIn.cs:27,49` | Sets Devil Square `MaximumPlayerCount` `10 -> 15` | Season 6/9 Devil Square (`diff-events-all.txt:33-34`: `max=15`) | `same` (`15`) |
| `EternalWingSetUpdatePlugIn.cs:51-92` | Fixes Eternal Wing (`#44`, Lv380 Summoner) drop levels (`116,131,122,102,99`), defense (`35,49,41,29,32`), requirements (`Lv380,Str62,Agi20,Ene170`), removes wrong attack speed on gloves | Season 6/9 `ItemList.xml` Lv 380 Eternal Wing set | Restores Season 6 set stats |
| `ExcellentDropChanceUpdatePlugIn.cs:30-56` | Bumps `SpecialItemType.Excellent` drop `Chance` `0.0001 -> 0.002` (`0.2%`) | Season 6 `CommonServer.cfg:45` (`ExcItemDropPer=20` `/10000` = `0.2%`) | `same` (`0.2%`) |
| `ExperienceLossRowsUpdatePlugIn.cs:50-112` | Replaces old death XP loss rows (`1%/5%/6%/7%..0.7%`) with current plugin defaults when unchanged | Season 6/9 PK death XP loss penalty tiers | Migration to updated defaults |
| `FieldSpawnCountsFromSuitesUpdatePlugIn.cs:12-203` | Aligns spawn spots of `22` field monsters across Maps `1,4,7,10,26,29,31,33,34,41` directly with suites | Season 6/9 `MonsterSetBase` | `same` (brings legacy DBs to suite spawn parity) |
| `FinishDarkKnightMasterTreePlugInSeason6.cs:55-159` | Updates Swell Life (`max=200%`, `Ene/20 + Vit/100 + Party*1%`; note `L61` `durationPerEnergy=1/180f`), Cold `ChainDrive`/`StrikeOfDestr`, Harmony `AddFinal` | Season 9 skill calc (`RegularSkillCalc.lua:55`) & `LifeSwellEffectInitializer.cs` | `same` Swell Life boost formula vs S9 (`differs` on `1/180` dur multiplier vs `1/10` in S9) |
| `FixChaosMixesPlugIn095D.cs`, `FixChaosMixesPlugInBase.cs`, `FixChaosMixesPlugInSeason6.cs` | Fixes 0.95d `+10/+11` rates (`50%/45%`), luck `+25%`, CrystalSword/PowerWaveScroll value (`1100`), Dark Horse/Raven Spirit drop (`0.001`) | Season 6 `ChaosBox` & `Spirit` drop groups | Repairs mix & drop regressions |
| `FixCharStatsForceWavePlugInBase.cs`, `FixCharStatsForceWavePlugInSeason6.cs:84-200` | Fixes DW agility/def (`0.25f`), MG BaseEnergy (`26`), DL HP/Mana (`90/40`, `1.5*Lv`, `2*Vit`), RF PvP def (`0.2*Agi+1.5*Lv`), AttackRate PvM/PvP, Fenrir (`Str/5+Vit/3`), Summoner def (`Agi/3`) | Season 6 character calc (`CalcCharacter.lua`) & `CharacterClassInitialization` | Aligns character stat multipliers |

Remaining files (`AddStardustItem*`, `AddWhiteWizardInvasionMobs*`, `ArenaGuardShop*`, `ArenaTitles*`, `ChainLightning*`, `ChaosCastleData*`, `CircularUpdateDependencyException`, `ConfigureCastleSiegeGuardTalkPlugIns*`, `ConfigureNpcTalkPlugIns*`, `DarkLordAttackRate*`, `DarkWizardTeleportSoulBarrier*`, `DataUpdateService`, `FenrirSecondClass*`, `FieldMonsterSpawnsUpdatePlugIn*`, `FinishDarkLordMasterTree*`, `FinishDarkWizardMasterTree*`, `FinishElfMasterTree*`, `FinishRageFighterMasterTree*`, `FinishSummonerMasterTree*`, `FixAncientDiscriminators*`, `FixAttackSpeedCalculationUpdate`, `FixChaosMixesUpdatePlugIn075`, `FixCharStatsForceWavePlugIn075`, `095d`) apply master-tree, attack-speed, shop, or service plumbing already reflected in current initializers.

## Re-check 2026-11-27 (changes 668ecbd4..a108e846)

Commits read (every changed file read in full):

- 7a58c00b Potion of Bless: raise the damage only against Castle Siege gates and statues

Evidence:

- BlessPotionSiegeOnlyUpdatePlugIn (Season 6, mandatory, idempotent: removes the power-ups of effect 10) - fixed. Style: its Display name/description are literals, not PlugInResources (rule 10).
- NEW (this re-check): commit 1fc60d72 fixed five data values in the initializers only (Kantata 10.0 -> 0.10 excellent damage chance, Guardian boots SD 200 -> 700, Hanzo store slots and Mace, Blue/Deep Blue box normal-item groups, GM test account slots). Existing databases never got them (rule 4). AuditDataFixesUpdatePlugInSeason6/075/095d added for Kantata, Guardian boots and Hanzo (PR "audit follow-ups 27 Nov"); the box groups need a rebuild of the normal-item group on existing databases - listed for main.
