# Audit Findings: `server/src/Persistence/Initialization/Version075 (files 1)`

- **Unit**: `server/src/Persistence/Initialization/Version075 (files 1)` (9 files, 1,386 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `6df27858`
- **Status**: `Checked`

## File-by-File Comparison

### `DataInitialization.cs` & `GameConfigurationInitializer.cs` & `GameMapsInitializer.cs`
- `DataInitialization.cs:20-63`: Version `0.75` (`Season = 0, Episode = 75`, client version `07500`, 8 classic maps: Lorencia, Dungeon, Devias, Noria, LostTower, Exile, Arena, Atlans in `GameMapsInitializer.cs:26-39`).
- `GameConfigurationInitializer.cs:28-63`: Classic option types (`Option` + `Luck` only; no Excellent/Ancient/Harmony/Socket); `CharacterClassInitialization.cs:28-36` enables `UseClassicPvp => true` for DK, DW, and Fairy Elf only.

### `ChaosMixes.cs`
- `ChaosMixes.cs:36-100`: Registers Chaos Weapon crafting (`ChaosWeaponAndFirstWingsCrafting`), `MoneyPerFinalSuccessPercentage = 10_000`, `NpcPriceDivisor = 20_000`, `MinimumItemLevel = 4`, requiring `+4` + Option + `1x Chaos`, optional Bless/Soul, producing `Chaos Dragon Axe`, `Chaos Nature Bow`, or `Chaos Lightning Staff` (`+0..+4`) -> matches classic MU / Season 6/9 suite Chaos Weapon mix fee & rules (**same**).

### `Gates.cs`
- `Gates.cs:43-59`: 14 warps (`Arena` Lv50/2000z, `Lorencia` Lv10/2000z, `Noria` Lv10/2000z, `Devias` Lv20/2000z, `Dungeon 1..3` Lv30..50/3000..4000z, `LostTower 1..7` Lv50..70/5000..8000z) match the shared warps in the Season 6/9 suites' warp table file (**same**).
- `Gates.cs:109-208`: Defines 0.75 enter/exit gates across maps `0, 1, 2, 3, 4, 6, 7`. Minor redundancy: `CreateEnterGate` (`Gates.cs:164,170`) sets `enterGate.LevelRequirement` twice.

### `SkillsInitializer.cs`
- `SkillsInitializer.cs:40-76`: Initializes 0.75 DW, DK, and Elf skills + `FlameOfEvil` monster skill.
- `SkillsInitializer.cs:95-125` (`Bali #150`): `Level=52, HP=5000, Dmg=165..170, Def=100, AttackRate=260, DefenseRate=75, MoveRange=3, AttackRange=1, ViewRange=7, MoveDelay=400ms, AttackDelay=1600ms, RespawnDelay=100s` -> matches the Season 6/9 suites' monster list (`Index="150"`, **same**; `WaterResistance=6/255` instead of `LightningRes=6`).

### `NpcInitialization.cs` & `MerchantStores.cs`
- `NpcInitialization.cs:46-271`: Initializes 0.75 NPCs (`200, 238..255`).
  - `CrossbowGuard (#247, 141-167)` & `BerdyshGuard (#249, 181-207)`: `Level=90, HP=10000, Dmg=180..195, Def=70, AttackRate=300, DefenseRate=100` match the Season 6/9 suites' monster list (**same**); `AttackRange=5/2, ViewRange=7, RespawnDelay=3s` **differs** from the suite monster list (`#247 AttackRange=2`, `#249 AttackRange=5`, `ViewRange=2/5`, `RegenTime=10s`).
- `MerchantStores.cs:112-150` (`CreateHanzoTheBlacksmith`):
  - **Bug / differs (`MerchantStores.cs:133`)**: Uses `(ItemGroups.Scepters, 1)` for `Mace` (`Morning Star` is `2:1` at `137`, while `Mace` is `2:0` in the Season 6/9 suites' item list files).
  - **Bug (`MerchantStores.cs:141-142`)**: Places both `Gladius` (`0:6`) and `Falchion` (`0:7`) at the same inventory `itemSlot = 73`.
