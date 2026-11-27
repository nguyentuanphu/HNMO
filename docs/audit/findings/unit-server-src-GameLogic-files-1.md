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

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- c5a7c888 Merge origin/fix/bless-potion-siege-only into the integration branch
- 7a58c00b Potion of Bless: raise the damage only against Castle Siege gates and statues
- 49b959ac fix(gamelogic): fix Fenrir skill check order, spawn gate, drop stack lock, and storage rollback

Evidence:

- AttackableExtensions: Potion of Bless x1.2 only when the defender is a CastleSiegeGate/CastleSiegeStatue (effect 10 = MagicEffectNumber.PotionOfBless) - same as the owner decision of 26 Nov (Q10) - fixed.
- AttackableExtensions: isSummonerSkill is now set only after a skill is known; a summoner's basic attack no longer counts as a curse skill - fixed (bug of the earlier code).
- CharacterInitialization: new characters spawn at ExitGate.GetRandomPoint() (inclusive bounds) instead of Rand.NextInt(X1,X2) which never reached X2/Y2 - fixed.
- DroppedItem: the bound-to-character and owner-time checks now run before the stack path, so a non-owner can no longer stack-pick an owned drop; partial and complete stacking re-check the target under lock(target) with CanPartiallyStackOn / CanCompletelyStackOn - fixed (race and ownership bypass).
