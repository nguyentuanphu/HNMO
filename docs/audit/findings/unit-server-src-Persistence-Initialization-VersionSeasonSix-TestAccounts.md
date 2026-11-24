# Audit Findings: `server/src/Persistence/Initialization/VersionSeasonSix/TestAccounts`

- **Unit**: `server/src/Persistence/Initialization/VersionSeasonSix/TestAccounts` (14 files, 2,079 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `0b4249a7`
- **Status**: `Checked`

## Comparison against Season 6 / Season 9 Suites

The Season 6 and Season 9 suites ship empty account tables in their SQL setup scripts; HNMO seeds 19 demo/test accounts (`test0..test9`, `test300`, `test400`, `ancient`, `socket`, `quest1..quest3`, `testgm`, `testgm2`, `testunlock`) via code (**differs**: HNMO test/demo accounts). All referenced item IDs, ancient set assignments, socket elements, and Marlon/class-quest references were cross-checked against the Season 6/9 suites' item list, ancient set, and socket files.

## File-by-File Findings

- `TestAccountsInitialization.cs:25-43`: Initializes `test0..test9` (`1..91`), `Level300`, `Level400`, `Ancient`, `Socket`, `Quest150`, `Quest220`, `Quest400`, `GameMaster`, `GameMaster2`, and `Unlocked`.
- `AccountInitializerBase.cs:24-35,247-288`:
  - `level > 220` attaches Marlon's completed quest state (`QuestConstants.LegacyQuestGroup`) and `Stats.IsSkillComboAvailable = 1` (**same** quest threshold as Season 6/9 suites' Marlon/ClassQuest config).
  - `AddAllSkills` excludes `EventSkills` (`Stun`, `CancelStun`, `SwellMana`, `Invisibility`, `CancelInvisibility`, `SpellofProtection`, `SpellofRestriction`, `SpellofPursuit`, `ShieldBurn`), `NovaStart`, weapon skills, and `Number >= 300` master skills.
  - `AccountInitializerBase.cs:232-233`: Spawn coordinate uses `Rand.NextInt(spawnGate.X1, spawnGate.X2)` / `(Y1, Y2)` (exclusive upper bound vs inclusive bounds in the Season 6/9 suites' gate settings).
  - Vault (`AccountInitializerBase.cs:776-816`) seeds `Invisibility Cloak` (`13:18`, +5..+8) and `Devil's Invitation` (`14:19`, +4..+7) — **same** item IDs as the Season 6/9 suites' item list files.
- `GameMaster.cs:32-73`:
  - **Bug (`GameMaster.cs:63,65`)**: `CreateDarkLord()` adds both `Broy Pendant of Ice` (`13:25`) at `InventoryConstants.PendantSlot` (`63`) and `Exc Pendant of Fire` (`13:13`) at `InventoryConstants.PendantSlot` (`65`), causing a duplicate equipped slot collision.
- `GameMaster2.cs:33-113`: Initializes `DimensionMaster` and `FistMaster` GM characters (`testgm2`), excluding gloves on `FistMaster` (Rage Fighter has no gloves slot — **same** as suite item slots).
- `Ancient.cs:31-131`: Equips `Agnis`/`Broy` (DL), `Hyon`/`Vicious` (BK), `Aruan`/`Gywen` (ME), `Enis`/`Anubis`/`Heras` (SM) — set-to-item assignments **same** as the Season 6/9 suites' ancient set type table (minor comment typo at `Ancient.cs:94`).
- `Socket.cs:32-163`: Equips `Royal`/`Hades`/`Titan`/`Brave`/`Seraphim` socket sets and weapons with 3–5 sockets -> matches Season 6/9 socket item definitions (**same**; copy-paste comments at `Socket.cs:119-123`).
- `Level300.cs`, `Level400.cs`, `LowLevel.cs`, `QuestBase.cs`, `Quest150.cs`, `Quest220.cs`, `Quest400.cs`: Item IDs and quest items (`Scroll of Emperor`, `Broken Sword`, `Soul Shard of Wizard`, `Tear of Elf`, `Feather/Flame/Horn` at `Quest400.cs:30-32`) match the Season 6/9 suites' quest & item lists (**same**). Note `LowLevel.cs:35-38,85-89` places DL/DW starter armor in bag slots rather than equipped slots.
- `Unlocked.cs:22-35`: Passes `level = 0`, unlocks all `CanGetCreated && CreationAllowedFlag > 0` classes (`testunlock`), and sets `AccountState.GameMaster`.
