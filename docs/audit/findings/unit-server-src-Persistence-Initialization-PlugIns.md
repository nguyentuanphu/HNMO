# Audit Findings: `server/src/Persistence/Initialization/PlugIns`

- **Unit**: `server/src/Persistence/Initialization/PlugIns` (20 files, 651 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `d52ea7ae`
- **Status**: `Checked`

## Overview

In the Season 6 and Season 9 suites, new-character starter equipment and starter skills are stored in the database (`DefaultClassType`) rather than flat config files; HNMO models them as `ICharacterCreatedPlugIn` plugins under `Persistence/Initialization/PlugIns/CharacterCreated/`.

## File-by-File Comparison

### Base PlugIns
- `AddInitialItemPlugInBase.cs:31-87`: Grants item `(itemGroup, itemNumber)` at `itemSlot` with `itemLevel` if the slot is empty and character class matches (or `null` for all classes).
- `AddInitialSkillPlugInBase.cs:24-65`: Grants `skillNumber` to `characterClassNumber` if qualified and not already learned.

### Starter Items (compared against the Season 6/9 suites' item list files)
- `AddSmallAxeForDarkKnight.cs:24`: `DarkKnight`, `(Axes=1, 0)`, slot `0` -> suite item list `Section 1, Index="0", Slot="0", DarkKnight="1"` (**same**).
- `AddShortBowForFairyElf.cs:24`: `FairyElf`, `(Bows=4, 0)`, slot `1` -> suite item list `Section 4, Index="0", Slot="1", FairyElf="1"` (**same**).
- `AddArrowsForFairyElf.cs:25,34`: `FairyElf`, `(Bows=4, 15)`, slot `0`, `Durability = 255` -> suite item list `Section 4, Index="15", Slot="0"` (**same**).
- `AddShortSwordForDarkLord.cs:24` & `AddShortSwordForMagicGladiator.cs:24`: `DarkLord` / `MagicGladiator`, `(Swords=0, 1)`, slot `0` -> suite item list `Section 0, Index="1", Slot="0"` (**same**).
- `AddSmallShieldForDarkLord.cs:24` & `AddSmallShieldForMagicGladiator.cs:24`: `DarkLord` / `MagicGladiator`, `(Shields=6, 0)`, slot `1` -> suite item list `Section 6, Index="0", Slot="1"` (**same**).
- `AddRingOfWarriorLevel40ForNewCharacters.cs:23` & `AddRingOfWarriorLevel80ForNewCharacters.cs:23`: all classes, `(13, 20)`, inventory slots `LastEquippableItemSlotIndex + 1` (level `1`) and `+ 2` (level `2`) -> `Ring of Warrior` (`13:20`) (**same** item ID; HNMO grants both rings via character-creation plugins).

### Starter Skills (compared against the Season 6/9 suites' skill list files)
- `AddEnergyBallForDarkWizard.cs:24`: `DarkWizard`, `EnergyBall` (`17`) -> suite skill list `Index="17", DarkWizard="1"` (**same**).
- `AddForceForDarkLord.cs:24`: `DarkLord`, `Force` (`60`) -> suite skill list `Index="60", DarkLord="1"` (**same**).
- `AddFireBlastForDarkLord.cs:24`: `DarkLord`, `FireBlast` (`74`) -> suite skill list `Index="74", DarkLord="1", Brand="75"` (**same**).
- `AddCrescentMoonSlashForDarkKnight.cs:24`: `DarkKnight`, `CrescentMoonSlash` (`44`) -> suite skill list `Index="44", DarkKnight="1", Brand="75"` (**same**).
- `AddStarfallForFairyElf.cs:24`: `FairyElf`, `Starfall` (`46`) -> suite skill list `Index="46", FairyElf="1", Brand="75"` (**same**).
- `AddSpiralSlashForMagicGladiator.cs:24`: `MagicGladiator`, `SpiralSlash` (`57`) -> suite skill list `Index="57", MagicGladiator="1", Brand="75"` (**same**).
- `AddManaRaysForMagicGladiator.cs:24`: `MagicGladiator`, `ManaRays` (Castle Siege skill) -> **same** class assignment.
- `AddLanceForSummoner.cs:24`: `Summoner`, `Lance` -> **same** class assignment.
- `AddChargeForRageFighter.cs:24`: `RageFighter`, `Charge` (`269`) -> suite skill list `Index="269", RageFighter="1"` (**same**).
