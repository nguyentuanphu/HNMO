# Audit Findings: `server/src/Persistence/Initialization/Version097d`

- **Unit**: `server/src/Persistence/Initialization/Version097d` (1 file, 53 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `2520096f`
- **Status**: `Checked`

## File-by-File Comparison

### `server/src/Persistence/Initialization/Version097d/Items/Jewels.cs`
- Extends `Version095d.Items.Jewels` (`Jewels.cs:15-32`) and adds `Jewel of Creation` (`Group = 14`, `Number = 22`, `Jewels.cs:38-52`).
- **Identity & Dimensions (`Jewels.cs:42-48`)**: `Group = 14`, `Number = 22`, `Width = 1`, `Height = 1`, `Durability = 1`.
  - Season 6 suite's item list file: `Section 14`, `Index="22"`, `Width="1"`, `Height="1"` -> **same**.
  - Season 9 suite's item list file: `Section 14`, `Index="22"`, `Width="1"`, `Height="1"` -> **same**.
- **Drop Level & Drop Source (`Jewels.cs:44-45,50`)**: `DropsFromMonsters = false`, `DropLevel = 72`, registered via `AddItemToJewelItemDrop(itemDefinition)` (global jewel drop pool).
  - Season 6 suite's item list file: `Drop="1"`, `DropLevel="78"` -> **differs** (HNMO sets `DropLevel = 72` and routes through the jewel item drop group rather than `DropsFromMonsters = true`).
  - Season 9 suite's item list file: `Drop="1"`, `DropLevel="78"` -> **differs** (same).
