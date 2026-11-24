# Audit: `server/src/GameLogic/Pet`

- **Unit**: `server/src/GameLogic/Pet`
- **Files read in full**: 6 of 6 (579 lines total)
  - `server/src/GameLogic/Pet/AttackerSurrogate.cs` (60 lines)
  - `server/src/GameLogic/Pet/IPetCommandManager.cs` (20 lines)
  - `server/src/GameLogic/Pet/PetLevelHelper.cs` (57 lines)
  - `server/src/GameLogic/Pet/RavenAttributeSystem.cs` (98 lines)
  - `server/src/GameLogic/Pet/RavenCommandManager.cs` (300 lines)
  - `server/src/GameLogic/Pet/UpdatePetCommandManagerOnItemMovePlugIn.cs` (44 lines)
- **Suite references**:
  - Season 6 suite: `the suite's pet settings file` (passive/ring pets), `the suite's character calc script` (Dark Raven behavior itself is in the GameServer binary)
  - Season 9 suite: `the suite's pet settings file`, `the suite's character calc script`

## Rules & Comparison

| Rule / Behaviour | HNMO | Season 6 / Season 9 Suite | Match |
|---|---|---|---|
| Dead pet command guard | `RavenCommandManager.SetBehaviourAsync` forces `PetBehaviour.Idle` when `_pet.Durability <= 0.0` | Dead Dark Raven (0 HP/durability) cannot attack | Same (PR #308 fix) |
| Attack speed & delay | `AttackDelay = max(100, 1500 - AttackSpeed * 10) ms` | Standard Dark Raven attack interval scaling with `RavenAttackSpeed` | Same |
| Attack modes & range | `AttackRange = 7`; `Idle`, `AttackRandom`, `AttackTarget` (reverts to `Idle` on target death), `AttackWithOwner`; ignores `NpcObjectKind.Guard` and skips while owner is in safezone | 4 standard Dark Spirit commands; range 7; safezone suppressed | Same |
| Single vs range attack | 30% `RangeAttack` (3 hits at `AttackDelay / 4` across up to 3 `AttackableNpcBase` within range 3 of `mainTarget`), 70% `SingleTarget` | Dark Raven single & area multi-hit (3 hits) | Same |
| Pet EXP curve | `PetLevelHelper.GetExperienceOfPetLevel` evaluates `ItemDefinition.PetExperienceFormula` via `mxparser` and caches per `(formula, maximumLevel)` | Formula-based pet level EXP table | Same (formula values checked in item/pet data) |
| Raven attribute mapping | `RavenAttributeSystem` maps `RavenMinimumDamage`/`RavenMaximumDamage` scaled by `RavenAttackDamageIncrease` (defaulting to `1` if `Attributes` is null), `RavenAttackSpeed`, `RavenAttackRate`, `RavenCriticalDamageChance`, `RavenExcDamageChance`, `DefenseIgnoreChance`, `ShieldBypassChance` | Standard Dark Raven stats + owner PvP/ignore options | Same |
| Equip/unequip hook | `UpdatePetCommandManagerOnItemMovePlugIn` resets to `Idle` on equip and removes `PetCommandManager` on unequip for non-defensive trainable pets | Resets Dark Spirit to Stay on equip/unequip | Same |
