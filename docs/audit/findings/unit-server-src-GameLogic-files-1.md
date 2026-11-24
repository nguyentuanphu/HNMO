# Audit Findings: `server/src/GameLogic (files 1)`

**Status**: Checked (all 30 files read in full)
**References**: [`combat-drops-exp-s6-s9.md`](../references/combat-drops-exp-s6-s9.md)

## Scope & Summary
Audited all 30 files (5,412 lines): `AccountExtensions`, `AttackableExtensions`, `BleedingMagicEffect`, `Bucket*`, `CharacterExtensions`, `CharacterInitialization`, `ComboState*`, `ConfigurationChangeMediator`, `DefaultDropGenerator`, `DeveloperHub*`, `DroppedItem`, `DroppedMoney`, `DuelRoom*`, `ExitGateExtensions`, `FeaturePlugInContainer`, etc.

## Rule & Bug Findings

1. **`isSummonerSkill` evaluated before `damageType = skill.DamageType` (`AttackableExtensions.cs:855-863`)**:
   - Line 855 sets `damageType = DamageType.Physical;` and line 856 checks `isSummonerSkill = attackerStats[Stats.MinimumCurseBaseDmg] > 0 && damageType != DamageType.Fenrir;` *before* line 863 assigns `damageType = skill.DamageType;`.
   - As a result, when a Summoner casts a Fenrir skill (`DamageType.Fenrir`), `isSummonerSkill` evaluates to `true`, skipping line 875's skill damage add and incorrectly adding Summoner curse/wizardry bonus lines (lines 902–917 and 959–963) on top of Fenrir damage.

2. **`CharacterExtensions.GetMaximumFruitPoints` assumes `Level` in `1..400` (`CharacterExtensions.cs:26-35`)**:
   - Indexes directly into 400-element arrays using `Level - 1` without bounds clamping; if `Level > 400` (possible only if `Level` itself isn't capped separately from `MasterLevel`), `IndexOutOfRangeException` occurs.

3. **`CharacterInitialization.InitializeNewCharacter` uses exclusive upper bound on spawn gate (`CharacterInitialization.cs:90-94`)**:
   - Calls `Rand.NextInt(spawnGate.X1, spawnGate.X2)` directly instead of `spawnGate.GetRandomPoint()` (`ExitGateExtensions.cs:25-30`, which uses `GetExclusiveUpperBound` so `X1 == X2` or `X2` works as expected). If a class home spawn gate has `X1 == X2`, `NextInt` receives an empty range or throws depending on `IRandomizer`.

4. **`DroppedItem.TryStackOnItemAsync` doesn't re-validate stack cap under lock (`DroppedItem.cs:115, 298-311`)**:
   - `CanCompletelyStackOn` is checked before acquiring `_pickupLock`; inside `_pickupLock`, `stackTarget.Durability += this.Item.Durability` executes unconditionally without re-checking `MaximumStackSize()`.

5. **Verified Core Parity & Safeguards**:
   - Duel damage factor = `0.6` (`AttackableExtensions.cs:312-317`, matching S6/S9 `DamageReduction = 60`).
   - SD shield ignores monster damage and `ShieldBypassChance`, respects `ShieldDecreaseRateIncrease` (`lines 341-351`).
   - Lightning knockback (`MoveRandomlyAsync`) and `ElementalModifierTarget` resist probability `1 / (resistance + 1)` with `>= 255` immunity (`lines 445-468`).
   - `DefaultDropGenerator` handles `ExtraRolls` item multipliers without inflating Zen (`lines 130-154`).
   - `DroppedMoney.TryPickUpByAsync` releases `_availableToPick` lock flag if no party member has inventory room (`lines 89-99`).
