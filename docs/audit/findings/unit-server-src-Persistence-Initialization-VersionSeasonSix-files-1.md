# Audit: `server/src/Persistence/Initialization/VersionSeasonSix (files 1)`

- **Unit**: `server/src/Persistence/Initialization/VersionSeasonSix (files 1)`
- **Files read in full**: 11 of 11 (5,487 lines total)
  - `CashShopInitializer.cs` (851), `ChaosMixes.cs` (1,328), `DataInitialization.cs` (73), `GameConfigurationInitializer.cs` (179), `GameMapsInitializer.cs` (105), `Gates.cs` (632), `HnmoArenaHeraldInitializer.cs` (94), `InvasionMobsInitialization.cs` (357), `MasterSkillPassivePowerUps.cs` (93), `MerchantStores.cs` (590), `NpcInitialization.cs` (1,179)
- **Suite references**:
  - Season 6 & Season 9 `the suite's chaos box config file`, `the suite's gate settings file`, `the suite's warp table file`, `the suite's shop list file`, `the suite's shops folder/*.xml`, `docs/audit/data/diff-{mixes,gates,shops,monsters,exp-events}.txt`, `docs/audit/data/s9/diff-{mixes,gates,shops,monsters,exp-events}.txt`

## Rules & Comparison

| Rule / Behaviour | HNMO | Season 6 / Season 9 Suite | Match |
|---|---|---|---|
| Chaos mixes (`ChaosMixes.cs`) | +10..+15 upgrades (60/60/55/55/50/50%, 2M..12M, Luck +25%, Exc/Anc/Guard -10%, Socket -20%), 1st/2nd/3rd wings (3rd wing Luck 10%), Dark Horse/Raven, Fenrir 1..4, Shield potions, Harmony/380, Cherry Blossom, Secromicon, `SocketWeaponMixItems` | S6/S9 `the suite's chaos box config file` (`diff-mixes.txt`) | Same except Mix 57 (`SocketWeaponMixItems` -> `SocketWeaponCrafting` differs from S9 `Mix.bmd`, see `Craftings` unit) |
| Gates, warps & duel config (`Gates.cs`) | All S6 enter/exit gates, warp list, Santa Village walkable safezone gate, `DuelConfiguration` (`MaxScore=10`, `MinLevel=30`, `Fee=30000`, Exit=294 Vulcanus, 4 areas) | S6/S9 `GateSettings.xml`, `MoveReq.xml` (`diff-gates.txt`) | Same (S9 maps/gates live in `Updates/SeasonNine`) |
| NPCs & merchant stores (`NpcInitialization.cs`, `MerchantStores.cs`) | All S6 NPCs, Elf Soldier buff (`3600s`, def `50+Lvl/5`, dmg `45+Lvl/3`, max lvl 220), 27+ merchant stores including Arena Guard (239), Moss (492), Leina (577), Bolo (578) | S6/S9 `ShopList.xml`, `Shops/*.xml` (`diff-shops.txt`) | Same (with Arena Guard/Moss per owner decision 21 Nov 2026) |
| Golden & White Wizard invasions (`InvasionMobsInitialization.cs`) | Golden Goblin (78), Golden Dragon (79), Golden Lizard King (80), Golden Vepar (81), Golden Tantallos (82), Golden Wheel (83), White Wizard (135), Destructive Ogre Soldier/Archer (136/137) + drops | S6/S9 `Monster.txt` (`diff-monsters.txt`) | Same |
| Global config, maps, Arena Herald, CashShop, Master passives | `MaximumAdditionalOptionLevel = 7`, `MaximumAllianceSize = 5`, 73 S6 maps, CashShop `512.2012.084`, `MasterSkillPassivePowerUps` (`-1/500` per level durability reduction), `HnmoArenaHeraldInitializer` (NPC 900) | S6 suite | Same (`HnmoArenaHeraldInitializer` is HNMO-specific) |

## Re-check 2026-11-27 (changes 8ef6758f..a108e846)

Commits read (every changed file read in full):

- 1fc60d72 fix(init): fix Kantata exc chance, Blue/DeepBlue boxes, Guardian boots SD, GM DL slots, and Hanzo store

Evidence:

- MerchantStores (Hanzo 251): same fix as 0.75 - fixed; existing databases by AuditDataFixesUpdatePlugInSeason6 (this re-check).
