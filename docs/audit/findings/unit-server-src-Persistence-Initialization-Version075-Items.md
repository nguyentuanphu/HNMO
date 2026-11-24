# Audit Findings: `server/src/Persistence/Initialization/Version075/Items`

- **Unit**: `server/src/Persistence/Initialization/Version075/Items` (11 files, 1,560 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `ecaa7c08`
- **Status**: `Checked`

## File-by-File Comparison

Cross-checked all 11 files against the Season 6/9 suites' item list files (`docs/audit/data/diff-items.txt`).

- `Constants.cs:15,20`: Sets `MaximumItemLevel = 11` and `MaximumOptionLevel = 4` (0.75 classic caps; later versions raise to `+15` / `+28`).
- `SlotTypesInitializer.cs:27-102`: Maps equipment slots `0..11` (Left/Right/Both Hands, Helm..Boots, Wings, Pet, Pendant, Rings).
- `Jewels.cs:39-96`:
  - `Jewel of Bless` (`14:13`, `DropLevel=25, Value=150`), `Jewel of Soul` (`14:14`, `DropLevel=30, Value=150`), `Jewel of Chaos` (`12:15`, `DropLevel=12`) -> `DropLevel` and `Value` **same** as the Season 6/9 suites' item list files; `DropsFromMonsters = false` (uses `AddItemToJewelItemDrop`) and `Durability = 1` **differs** from suite `Drop="1"` / `Durability="0"` (`diff-items.txt:50`).
- `Pets.cs:35-46`:
  - `Guardian Angel` (`13:0`, `ReqLvl=23`, `DamageReceiveDecrement=0.8f`, `+50 MaxHP`), `Imp` (`13:1`, `ReqLvl=28`, `AttackDamageIncrease=1.3f`), `Horn of Uniria` (`13:2`, `ReqLvl=25`, `Durability=255`) -> **same** level, durability, and slots as the Season 6/9 suites' item list files.
- `Wings.cs:40-133`:
  - `Wings of Elf` (`12:0`, `3x2, Def=10, ReqLvl=180, Dur=200`), `Wings of Heaven` (`12:1`, `5x3, Def=10, ReqLvl=180, Dur=200`), `Wings of Satan` (`12:2`, `5x2, Def=20, ReqLvl=180, Dur=200`) -> dimensions, defense, durability, and base required level **same** as the Season 6/9 suites' item list files (`12%` initial damage increase/absorb + per-level tables).
  - Unimplemented rule (`Wings.cs:107`): `TODO` notes per-level `+5` level requirement increase is not added (**differs** from standard suite wing level scaling).
- `Orbs.cs:34-41` & `Scrolls.cs:33-47`:
  - Elf Orbs (`12:8..11`) and DW Scrolls (`15:0..11`) match drop levels, energy requirements (`OrbRequirements` / `TotalEnergyRequirementValue`), and Zen values (**same**); `Durability = 1` **differs** from suite `0` (`diff-items.txt:43-46`).
- `Potions.cs:31-238`:
  - Apple (`14:0`, `Lv1`), Healing/Mana Potions (`14:1..6`, `Lv10/25/40`, `Durability=3`), Antidote (`14:8`), Ale (`14:9`), Town Portal Scroll (`14:10`, `Lv30`, `1x2`) -> **same** dimensions, drop levels, and IDs as the Season 6/9 suites' item list files.
- `Jewelery.cs:47-67`:
  - `Ring of Ice` (`13:8`, `Lv20`), `Ring of Poison` (`13:9`, `Lv17`), `Pendant of Lightning` (`13:12`, `Lv21`), `Pendant of Fire` (`13:13`, `Lv13`), `Transformation Ring` (`13:10`, `ReqLvl=20`) -> **same**; `Jewelery.cs:175` notes per-level requirement scaling is also left as `TODO`.
- `Armors.cs:33-140` & `Weapons.cs:84-342`:
  - Classic shields (`6:0..14`), helm/armor/pants/gloves/boots sets (`0..14`), and weapons (`0:0..15`, `1:0..8`, `2:0..6`, `3:0..9`, `4:0..15`, `5:0..7`) match shared IDs, dimensions, damage, speed, durability, and class flags in the Season 6/9 suites' item list files (**same**; `Weapons.cs:323` attaches `CrystalSwordIceChance = 1/20` on `2:5`, and `316` initializes `StaffRise` = `magicPower / 2.0f` plus even/odd bonus tables).
