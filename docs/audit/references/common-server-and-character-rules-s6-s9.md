# Season 6 & Season 9 Common Server & Character Rules Reference

Extracted from Season 6 and Season 9 suite `CommonServer.cfg` and `Common` INI files.

## 1. Levels, Master System & Stats
| Setting | Season 6 | Season 9 | Notes |
| --- | --- | --- | --- |
| `MaxNormalLevel` | `400` | `400` | Max regular character level |
| `MaxMasterLevel` | `200` | `330` | S6E3=200; S8E3/S9=330 |
| `MasterPointPerLevel` | `1` | `1` | Master skill points per level |
| `MonsterMinLevelForMLExp` | `95` | `95` | Min monster level to grant master EXP |
| `LevelUpPointNormal` | `5` | `5` | Standard classes (DW/DK/Elf/SUM) |
| `LevelUpPointMGDL` | `7` | `7` | Bonus classes (MG/DL; RF) |
| `MaxStrength` / `Agility` / `Vitality` / `Energy` / `Command` | `65535` each | `65535` each | Engine ceiling per stat |
| `UseCharacterAutoRecuperationSystem` | `1` (max lv `100`) | `1` (max lv `100`) | Starter auto-regen below max level |

## 2. Helper (`MuBot`), Trade, Items & Upgrading
| Setting | Season 6 | Season 9 | Notes |
| --- | --- | --- | --- |
| `MuBot Enable / MinLevel` | `1` / `80` | `1` / `80` | Min level to activate helper |
| `MuBot Cost` | `20000` (formula: `LEVEL * COST`) | `20000` (`LEVEL * COST`) | Zen charged per helper stage |
| `ZenDropMultipler` | `1.0` | `1.0` | `Money + exp/BaseMasterExperience * ZenDropMultipler` |
| `UseSoulRate` / `UseSoulWithLuckRate` | `50%` / `75%` | `50%` / `75%` | Jewel of Soul success rates |
| `UseLifeRate` / `Is28Option` | `50%` / `1` (28 max opt) | `50%` / `1` (28 max opt) | Jewel of Life max +28 |
| `PotionDelayTime` | `100` ms | `100` ms | Minimum delay between potions |
| `ZenDurationTime` / `ItemDisapearTime` / `LootingTime` | `10s` / `10s` / `10s` (CommonServer `15s`/`20s`/`10s`) | `10s` / `10s` / `10s` | Ground drop ownership & despawn |
| `CanUseSocketExcItem` | `1` (enabled, no monster drop) | `1` (enabled, no monster drop) | Excellent + Socket overlap |
| `CanUseAncHarmonyItem` | `1` | `1` (marked S6E3 only) | Harmony + Ancient overlap |
| `AllowMoveCashItems` | N/A | `0` | Trade/vault restriction on Cash Shop / Lucky items |
| `LuckyItemDurabilityTime` | `2400` s | `2400` s | Seconds per durability point loss |
| `IsCancelItemSale` | N/A | `1` (`110.0%` price, `86400s` expiry) | S9 NPC buyback |

## 3. Party, Guild, Duel, Buffs & Reset
| Setting | Season 6 | Season 9 | Notes |
| --- | --- | --- | --- |
| `NormalParty` EXP Bonus (2/3/4/5) | `160` / `180` / `200` / `220` | `160` / `180` / `200` / `220` | Percentage EXP multiplier |
| `SetParty` EXP Bonus (3/4/5) | `230` / `270` / `300` | `230` / `270` / `300` | Class-diverse set party bonus |
| `GuildCreateLevel` | `150` | `150` | Min level to create guild |
| `MaxGuildMember` | `80` | `80` | Max members per guild |
| `AllianceMinGuildMember` / `AllianceMaxGuilds` | `10` / `3` | `10` / `3` | Guild alliance thresholds |
| `DuelSystem DamageReduction` | `60%` | `60%` | Damage reduction inside Duel Arena |
| `ShadowPhantomMaxLevel` | `220` | `220` | Elf Soldier NPC buff level ceiling |
| `ShieldSystemOn` / `DamageDevideToSD:HP` | `1` / `90:10` | `1` / `90:10` | PvP SD/HP split, `SuccessAttackRateOption = 20000` |
| `PkClearCost` / `Multipler` | `1000000` Zen / `1` | `1000000` Zen / `1` | `PkClearCost * PK_Count` |
| `ResetCMD` defaults | `MinLevel=400`, `Cost=10M`, `MaxReset=70`, `PointPerReset=200` | `MinLevel=400`, `Cost=50M`, `MaxReset=70`, `PointPerReset=200` | Disabled (`Enable=0`) by default |
