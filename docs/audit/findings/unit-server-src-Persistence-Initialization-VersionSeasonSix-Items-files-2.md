# Unit Audit: `server/src/Persistence/Initialization/VersionSeasonSix/Items (files 2)`

- **Checked at**: `f1e56a23` (2026-11-24)
- **Status**: `Checked`
- **Reference saved**: [socket-options-and-pets-s6-s9.md](../references/socket-options-and-pets-s6-s9.md)
- **Files read in full (8)**:
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Pets.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Potions.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Quest.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Scrolls.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/SocketSystem.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/SocketWeaponMixItems.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Weapons.cs`
  - `server/src/Persistence/Initialization/VersionSeasonSix/Items/Wings.cs`

## Comparison & Findings
1. **Socket Options (`SocketSystem.cs`) vs S6 & S9 Suites**:
   - Every Fire, Ice, Lightning, Water, Wind, Earth value (levels 1–5), Socket Bonus option (`30%`), and Package Bonus (`DoubleDamageChance +3%`, `DefenseIgnoreChance +1%`) matches S6 `SocketOptionSettings.ini` 1:1.
   - Minor code note: `CreateRelatedSocketOption` (`SocketSystem.cs:501-529`, used for Fire `(Level) Attack/Wizardry Increase`) populates `LevelDependentOptions` without assigning a fallback `itemOption.PowerUpDefinition` at level 1 (unlike `CreateSocketOption`).
   - Recorded S9 differences (`IGC_SocketOption.xml`) in the reference doc for Season 9 reconciliation: Water Lv5 Shield Defense (`30%` vs `20%`), Wind Max AG (`50..130` vs `25..50`), Wind AG Value (`5..13` vs `3..15`), and Seed Lv4 Bonus Options (`22/22/10/22/27/31`).
2. **Jack O'Lantern Blessings Duration (`Potions.cs:485`) — Minor Discrepancy**:
   - `CreateJackOLanternBlessings()` (`14:46`) sets `TimeSpan.FromMinutes(32)` (`1920s`), whereas S6 `IGC_ItemOptionManager.xml` specifies `Time="1800"` (30 minutes, matching `14:47..50` which all use `TimeSpan.FromMinutes(30)`).
3. **Lv380 Guardian Weapon Mapping (`Weapons.cs:536-555`)**:
   - All seven Lv380 weapons (`0:22`, `0:23`, `0:35`, `2:14`, `4:21`, `5:12`, `5:19`) match S6/S9 `(Group, Number)` Lv380 weapon sets.
4. **Socket Weapon Mix (`SocketWeaponMixItems.cs`)**:
   - Ensures Creation Notes (`14:191..203`), Lv400 5-socket weapons, `ItemRules.Apply`, `ItemStatProfiles.Apply`, and Chaos Machine crafting `#57` (see `unit-server-src-GameLogic-PlayerActions-Craftings.md` for `#57` recipe comparison against S9 `Mix.bmd`).
5. **Pets, Quest Items, Scrolls, Wings (`Pets.cs`, `Quest.cs`, `Scrolls.cs`, `Wings.cs`)**:
   - Pet stats, Dark Horse/Raven formulas (`level^3 * 100 * (level + 10)`, max 50), Fenrir variants/speed bonuses, quest item flags (`IsBoundToCharacter`, `IsQuestItem`, `StorageLimitPerCharacter = 1`), and wing tables match S6 reference rules cleanly.
