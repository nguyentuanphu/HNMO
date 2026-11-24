# Audit Findings: `server/src/GameLogic/Attributes`

- **Unit**: `server/src/GameLogic/Attributes` (9 files, 2,871 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference saved**: [attributes-and-stats-s6-s9.md](../references/attributes-and-stats-s6-s9.md)

## Files Reviewed (in full)
1. `AccountAttributeGrantPowerUps.cs` (1–147)
2. `AttributeSystemExtensions.cs` (1–102)
3. `ItemAwareAttributeSystem.cs` (1–159)
4. `MapPowerUps.cs` (1–69)
5. `MonsterAttributeHolder.cs` (1–238)
6. `PowerUpWrapper.cs` (1–125)
7. `SkillAttributeSystem.cs` (1–58)
8. `Stats.cs` (1–1865)
9. `TrapAttributeHolder.cs` (1–103)

## Rules & Behavior Summary
- `AccountAttributeGrantPowerUps`: Applies timed account attribute grants (`AddRaw`), tracks earliest expiry with timer rescheduling capped at `uint.MaxValue - 1` ms, and removes expired grants cleanly on timer or sync.
- `AttributeSystemExtensions`: `CreateDurationElement`, `CreateChanceElement`, and `CreateElement` build `ComposableAttribute` instances from base constant values plus `AttributeRelationship` elements while preserving `MaximumValue`.
- `ItemAwareAttributeSystem`: Merges `character.Attributes` and `account.Attributes` with `DistinctBy(a => a.Definition)` (character stats take priority over account stats), manages item/set power-up lifetimes, and tracks item set power-ups separately.
- `MapPowerUps`: Adds/removes `GameMapDefinition.CharacterPowerUpDefinitions` when entering/leaving maps.
- `MonsterAttributeHolder`: Caches static `MonsterDefinition.Attributes` in a shared mapping, applies `SpawnArea.MaximumHealthOverride` when set, dynamically scales `DefensePvm`/`DefensePvp` by `1 + SummonedMonsterDefenseIncrease`, initializes `DamageReceiveDecrement=1`, `AttackDamageIncrease=1`, `MovementSpeedFactor=1`, `ShieldBypassChance=1`, and derives `DefenseDecrement = 1 - InnovationDefDecrement`.
- `TrapAttributeHolder`: Caches trap monster attributes with `AttackDamageIncrease=1` and `ShieldBypassChance=1`.
- `SkillAttributeSystem`: Maintains per-skill `SkillLevel` alongside `MasterSkillValue` and passive skill power-ups.
- `Stats`: Defines all core attribute GUIDs, caps (`AttackSpeed=200`, `MagicSpeed=200`, `SoulBarrierReceiveDecrement=0.7f`, `ShieldRecoveryRampFactor=3`, boolean flags max `1`), elemental resistance-to-damage-bonus mappings, and regeneration descriptors (`Mana`, `Health`, `Ability`, `Shield`, and after-kill variants).

## Observations & Potential Discrepancies
1. **Needs runtime check — `Stats.ManaRegeneration.IntervalResting` (`Stats.cs:1747-1750`)**: `ManaRegeneration` leaves `Interval` at the default `3s` (`Regeneration.Interval = TimeSpan.FromSeconds(3)`) while setting `IntervalResting = TimeSpan.FromSeconds(5)`. Because `Regeneration.Interval` represents how fast a full regeneration cycle completes (see `HealthRegeneration`: `Interval = 7s`, `IntervalResting = 5s`, where resting is faster), `5s` while resting makes mana regeneration slower when sitting/resting unless compensated elsewhere.
2. **Speed cap (`Stats.cs` `AttackSpeed` / `MagicSpeed` = `200`)**: Hard cap at `200` enforces client packet/animation bounds cleanly; S6/S9 reference scripts leave speed calculation uncapped in Lua (`Dex / divisor`) unless capped by anti-hack/client limits.
3. **S9 class readiness (`Grow Lancer`)**: Attribute definitions in `Stats.cs` currently model the 7 Season 6 classes + standard equipment categories (`OneHandedSword`, `TwoHandedSword`, `OneHandedStaff`, `TwoHandedStaff`, `Stick`, `Book`, `Scepter`, `GloveWeapon`); no lance/shield-gloves/Mount attributes yet for S9 classes if enabled later.
