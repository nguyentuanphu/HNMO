# Combat, Drops & Experience Core Formulas Reference (HNMO / S6 & S9 Parity Notes)

Derived from reading `AttackableExtensions.cs`, `DefaultDropGenerator.cs`, `CharacterExtensions.cs`, `ComboStateMachine.cs`, and comparing against S6/S9 suite behavior (`the suite's Castle Siege config file` Duel/CS damage reductions, CalcCharacter).

## 1. Hit Chance Formulas
- **PvM**:
  - Effective defense rate: `DefenseRatePvm + IncreaseBlockBonus`
  - If `defenseRate < attackRate`: `hitChance = 1.0f - (defenseRate / attackRate)`
  - Otherwise (`defenseRate >= attackRate`): `hitChance = 0.03f` (3% minimum hit floor, and `dmg` takes `0.3` overrate factor)
- **PvP**:
  - `rateFactor = attackRate / (attackRate + defenseRate)` (0.5 if sum is 0)
  - `levelFactor = attackLevel / (attackLevel + defenseLevel)` (using `TotalLevel = Level + MasterLevel`)
  - `hitChance = 2.0f * rateFactor * levelFactor`
  - Level gap penalty (`defenseLevel - attackLevel`): `>=300 -> -0.15`, `>=200 -> -0.10`, `>=100 -> -0.05`, clamped `[0, 1]`.

## 2. Damage Calculation Order (`CalculateDamageAsync`)
1. Roll `CriticalDamageChance`, `ExcellentDamageChance`, `DefenseIgnoreChance`.
2. Effective defense (if not ignored): `(DefensePvm|DefensePvp + GreaterDefenseBonus) * DefenseDecrement`.
3. Base dmg roll (`GetBaseDmg` -> `GetSkillDmg`):
   - **Physical**:
     - Excellent: `baseMax * 1.2 + ExcDmgBonus + CritDmgBonus + BerserkerMaxPhysDmgBonus`
     - Critical: `baseMax + CritDmgBonus + BerserkerMaxPhysDmgBonus`
     - Normal: random `[baseMin + BerserkerMin, baseMax + BerserkerMax]`
     - If `HasDoubleWield > 0`: `dmg += dmg`
     - `dmg = dmg * duelFactor - defense` (`duelFactor = 0.6` for active duel opponent)
     - `+ TwoHandedWeaponDamageIncrease` (if two-handed)
     - Surrogate (Dark Raven): `+ RavenBonusDamage`, non-crit/non-exc divided by `1.5`.
   - **Wizardry / Curse / Fenrir**:
     - `(baseMax * duelFactor - defense)` * `1.2 + ExcDmgBonus` (Excellent), or `+ CritDmgBonus` (Critical), or random roll `* duelFactor - defense`.
4. Add `GreaterDamageBonus`, Weakness debuff, Monster Overrate (`* 0.3` PvM when `DefenseRatePvm > AttackRatePvm`), `ArmorDamageDecrease`.
5. Minimum floor damage: `Math.Max(1, attackerLevel / 10)` (before `AttackDamageIncrease` / `DamageReceiveDecrement`).
6. Skill final bonus/multiplier (or Dinorant `* 1.3` on basic attack), Soul Barrier reduction (`Clamp(0..0.9)`) & `ManaTollPerHit`.
7. Add `FinalDamageBonus`, PvP bonuses (`FinalDamageIncreasePvp` + Master PvP weapon mastery), Chaos Castle PvP halving (`dmg /= 2`, halved again for non-SD `isClassicPvp`).
8. If `dmg > 0`: add `ComboBonus`, then `DoubleDamageChance` (`dmg *= 2`), then `RateTuningPlugIn` and `CombatLimitsPlugIn`.
9. SD Shield Split (`GetHitInfo`):
   - Monsters and `ShieldBypassChance` procs hit HP directly (`shieldDamage = 0`).
   - Otherwise `shieldRatio = max(0, ShieldRatio - ShieldDecreaseRateIncrease)`: HP gets `dmg * (1 - shieldRatio)`, SD gets `dmg * shieldRatio`.

## 3. Experience & Drop Formulas
- **Base Monster EXP (`CalculateBaseExperience`)**:
  - Summoned monsters (`SummonedBy != null`): `0`.
  - `(targetLevel + 25) * targetLevel / 3.0`, scaled by `(targetLevel + 10) / killerLevel` if `killerLevel > targetLevel + 10`, plus `(targetLevel - 64) * (targetLevel / 4)` if `targetLevel >= 65`, multiplied by `1.25`.
- **Standard Item Drops (`DefaultDropGenerator`)**:
  - `DropLevelMaxGap = 12`, `SkillDropChance = 50%`, `DefaultMaxItemOptionLevel = 3` (`Min 1..Max 4`), `BaseMoneyDrop = 7 + gainedExp`.
  - Item level from monster level: `min((monsterLevel - DropLevel) / 3, MaximumItemLevel)`.
  - Excellent items: `monsterLevel - ExcellentItemDropLevelDelta`, excludes socketed items, always `HasSkill`, 1st excellent opt guaranteed + remaining at `AddChance`.
  - Ancient items: always `HasSkill`, random ancient set + `BonusOption` level.
