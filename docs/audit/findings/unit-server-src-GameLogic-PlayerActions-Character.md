# Audit Findings — `server/src/GameLogic/PlayerActions/Character`

- **Unit**: `server/src/GameLogic/PlayerActions/Character` (8 files, 598 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `AddMasterPointAction.cs`, `CreateCharacterAction.cs`, `DeleteCharacterAction.cs`, `FocusCharacterAction.cs`
  - `IncreaseStatsAction.cs`, `RequestCharacterListAction.cs`, `SaveKeyConfigurationAction.cs`, `SelectCharacterAction.cs`

## Rules & Cross-Check
| Rule | HNMO Implementation | Season 6 / 9 Suite Reference | Status |
| --- | --- | --- | --- |
| Master skill prerequisite level | `AddMasterPointAction.cs:14`: `MinimumSkillLevelOfRequiredSkill = 10` (both previous rank in root and `RequiredMasterSkills` require level >= 10, or base non-master skill learned) | Suite master skill tree requires 10 points in prerequisite node / rank | Same |
| Unlock cost for level-0 master skill | `AddMasterPointAction.cs:77-78,94`: `requiredPoints = learnedSkill.Level == 0 ? MinimumLevel : 1`; adds `requiredPoints` to `learnedSkill.Level` and subtracts from `MasterLevelUpPoints` | S6/S9 `MinimumLevel` (1..3 points depending on tier) spent on first rank | Same |
| Bonus class unlock level on creation | `CreateCharacterAction.cs:54-63,99-108`: enforces `characterClass.LevelRequirementByCreation` (`UnlockedCharacterClasses` or any character >= required level); notes MG=220, DL=250, RF=150 | Class unlock level requirements enforced at character creation | Same |
| Character deletion guards | `DeleteCharacterAction.cs:48-63`: validates `SecurityCode` (falls back to BCrypt `PasswordHash` if `SecurityCode` is empty) and blocks deletion while in a guild (`GuildPosition != Undefined`) | Guild membership blocks deletion; security code checked | Same |
| Stat increase ceiling | `IncreaseStatsAction.cs:23-65`: verifies `CanIncreaseStats(amount)`, `IncreasableByPlayer`, and clamps to `Attribute.MaximumValue` (`65535` etc.) | `CommonServer.cfg`: `MaxStrength/Agility/Vitality/Energy/Command = 65535` | Differs on edge case (see Bug #1) |
| Character selection bans & Arena | `SelectCharacterAction.cs:53-83`: checks active/persisted character ban via `BanManagerService` with bilingual EN/VI notice, applies Arena template if `ArenaPlugIn` active | HNMO-specific moderation & Arena integration | Custom (HNMO) |

## Real Bugs & Edge Cases
1. **Bug — `IncreaseStatsAction.cs:44-50` (`ushort` underflow when `current > maximumValue`)**:
   - Line 46 checks `current + amount > maximumValue`, then line 48 computes `amount = (ushort)(maximumValue - current)`.
   - If `current` already exceeds `maximumValue` (for example from a GM adjustment or lowered cap), `maximumValue - current` is negative. Casting a negative `float` directly to `(ushort)` in unchecked C# wraps to a huge positive value (~`65535`), bypassing the `if (amount == 0)` guard on line 49 and adding wrapped stat points if `LevelUpPoints` allows. Should guard `if (current >= maximumValue)` first (`amount = 0`).
2. **Observation — `AddMasterPointAction.cs:53-65`**:
   - Does not explicitly reject characters who are not master-class yet (`MasterLevelUpPoints < 1` on line 30 prevents spending unless points were somehow granted via GM/reset).
