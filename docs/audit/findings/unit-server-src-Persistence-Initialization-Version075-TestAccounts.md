# Audit Findings: `server/src/Persistence/Initialization/Version075/TestAccounts`

- **Unit**: `server/src/Persistence/Initialization/Version075/TestAccounts` (3 files, 658 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `e8eeb6eb`
- **Status**: `Checked`

## Comparison against Season 6 / Season 9 Suites

The Season 6 and Season 9 suites ship empty account tables (`MEMB_INFO` / `Character`) in their database scripts; HNMO seeds demo accounts (`test0`..`test9`) via code (**differs**: HNMO test/demo feature). All referenced item group/number constants were cross-checked against the Season 6/9 suites' item list files.

## File-by-File Findings

- `TestAccountsInitialization.cs:25-32`: Creates 10 accounts (`test0`..`test9`) with character levels `1, 21, ..., 181`.
- `AccountInitializerBase.cs:61-177`:
  - Grants `10,000,000` Zen (`AccountInitializerBase.cs:175`), `BCrypt` password hash matching account name (`65`), and `(level - 1) * PointsPerLevelUp` unspent stat points (`172-173`).
  - Spawn position (`162-163`) uses `Rand.NextInt(spawnGate.X1, spawnGate.X2)` / `(Y1, Y2)`, which excludes `X2`/`Y2` (minor off-by-one vs inclusive gate bounds in the Season 6/9 suites' gate settings).
- `AccountInitializerBase.cs:189-517` & `LowLevel.cs:31-106`:
  - Referenced items: `Small Axe` (`1:0`, `LowLevel.cs:93`), `Short Bow` (`4:0`, `LowLevel.cs:102`), `Arrows` (`4:15`, `AccountInitializerBase.cs:466`), `Skull Staff` (`5:0`, `LowLevel.cs:84`), `Leather`/`Vine`/`Pad` armor sets (`groups 7..11`, `numbers 5/10/2`, `LowLevel.cs:35-39,53-57,71-75`), `Jewel of Bless` (`14:13`), `Jewel of Soul` (`14:14`), `Jewel of Chaos` (`12:15`), potions (`14:0..6, 9`), pets (`13:0..2`), and rings/pendants (`13:8,9,12`) -> all item Group/Number IDs **same** as the Season 6/9 suites' item list files.
  - Note: `LowLevel.cs:35-39` (`CreateKnight`) and `LowLevel.cs:71-75` (`CreateWizard`) place armor in bag slots `52, 47, 49, 63, 65` instead of equipped slots (`InventoryConstants`), whereas `CreateElf` (`LowLevel.cs:53-57`) equips them directly.
