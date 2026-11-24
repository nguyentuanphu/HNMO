# Reference: Skill Cooldowns (`S6`/`S9` vs HNMO) & `Player` Core Lifecycle

> Sanitized reference captured during the `server/src/GameLogic (files 4)` audit (2026-11-24) so future audits do not need to re-read `Player.cs` (2,778 lines), `SkillDelays.cs`, or legacy skill cooldown tables.

## 1. Legacy Suite Skill Cooldowns (`S6` & `S9`) vs `HNMO` (`SkillDelays.cs`)
Every non-zero cooldown in both Season 6 and Season 9 legacy suite skill lists matches identically between S6 and S9:

| Skill ID | Name | S6 / S9 Delay (ms) | HNMO (`SkillDelays.cs`) |
| --- | --- | --- | --- |
| `62` | Earthshake | `10000` | `10000` |
| `63` | Summon | `30000` | `30000` |
| `65` | Electric Spike | `5000` | `5000` |
| `76` | Plasma Storm | `1500` | `1500` |
| `217` | Damage Reflection | `5000` | `5000` |
| `218` | Berserker | `5000` | `5000` |
| `221` | Weakness | `4000` | `4000` |
| `222` | Innovation | `4000` | `4000` |
| `223` | Explosion | `4000` | `4000` |
| `224` | Requiem | `4000` | `4000` |
| `225` | Pollution | `4000` | `4000` |
| `262` | Chain Drive | `700` | Excluded intentionally (client sends 2 hits per cast) |
| `264` | Dragon Roar | `700` | `700` |
| `265` | Dragon Slasher | `3000` | `3000` |
| `270` | Phoenix Shot | `1200` | `1200` |
| `344` | Blood Storm | `10000` | `10000` |
| `346` | Blood Storm Strengthener | `10000` | `10000` |
| `425` | Cure | `3000` | `3000` |
| `426` | Party Healing | `10000` | `10000` |
| `427` | Poison Arrow | `10000` | `10000` |
| `429` | Party Healing Strengthener | `10000` | `10000` |
| `430` | Bless | `3000` | `3000` |
| `433` | Bless Strengthener | `3000` | `3000` |
| `434` | Poison Arrow Strengthener | `10000` | `10000` |
| `441` | Infinity Arrow Strengthener | `200` | `200` |
| `459` | Weakness Strengthener | `4000` | `4000` |
| `460` | Innovation Strengthener | `4000` | `4000` |
| `461` | Blind | `10000` | `10000` |
| `463` | Blind Strengthener | `10000` | `10000` |
| `469` | Berserker Strengthener | `5000` | `5000` |
| `470` | Berserker Proficiency | `5000` | `5000` |
| `472` | Berserker Mastery | `5000` | `5000` |
| `495` | Earth Prison | `10000` | `10000` |
| `497` | Earth Prison Strengthener | `10000` | `10000` |
| `512` | Earthshake Strengthener | `10000` | `10000` |
| `516` | Earthshake Mastery | `10000` | `10000` |
| `519` | Electric Spark Strengthener | `5000` | `5000` |
| `521` | Iron Defense | `60000` | `60000` |
| `524` | Iron Defense Strengthener | `60000` | `60000` |
| `558` | Chain Drive Strengthener | `700` | Excluded intentionally (client sends 2 hits per cast) |
| `560` | Dragon Roar Strengthener | `700` | `700` |
| `561` | Dragon Roar Mastery | `700` | `700` |
| `562` | Chain Drive Mastery | `700` | Excluded intentionally (client sends 2 hits per cast) |
| `564` | Dragon Slasher Strengthener | `3000` | `3000` |
| `565` | Blood Howling | `120000` | `120000` |
| `566` | Dragon Slasher Mastery | `3000` | `3000` |
| `567` | Blood Howling Strengthener | `120000` | `120000` |
| `618` | Spell of Protection | `60000` | `60000` |
| `619` | Spell of Restriction | `60000` | `60000` |
| `620` | Spell of Pursuit | `120000` | `120000` |
| `621` | Shield-Burn | `60000` | `60000` |

- `SkillDelays.ToleranceFactor = 0.8` (checks `now - lastUse >= delay * 0.8`).

## 2. HNMO `Player` Combat, Durability & Lifecycle Constants (`GameLogic (files 4)`)
- **Enter world (`Player.SetReclaimableAttributesBeforeEnterGame`)**:
  - `CurrentShield = MaximumShield`, `CurrentMana = MaximumMana`, `CurrentAbility = MaximumAbility / 2`, `CurrentHealth = min(CurrentHealth, MaximumHealth)`.
  - GameMaster (`CharacterStatus.GameMaster`) receives invisible-to-death effect `Number = 28` (`"GM MARK"`).
- **On hit (`Player.AttackByAsync` / `HitAsync`)**:
  - `CrystalSwordIceChance` can force skill `7` (Ice).
  - Freeze broken/shortened by 1s (`ShortenEffectsProducingSpecificStatAsync(Stats.IsFrozen, 1)`); Sleep cleared on damage (`ClearAllEffectsProducingSpecificStatAsync(Stats.IsAsleep)`).
  - Reflect: `500 ms` delayed, not triggered by `DamageAttributes.Poison` or `DamageAttributes.Reflected`. Full reflect excludes `AttackerSurrogate` (Raven).
  - Late damage (`ReflectDamageAsync`, `ApplyPoisonDamageAsync`, `ApplyBleedingDamageAsync`) blocked when target is at safezone (`!ReferenceEquals(attacker, this)`).
  - `PoisonMagicEffect`: ticks every `3000 ms`, damage = `CurrentHealth * attacker[Stats.PoisonDamageMultiplier]`.
- **Durability loss (`Player.cs:2500-2765`)**:
  - `ItemDurabilityProtection` skips all items except `PetSlot` and trainable pets.
  - Armor/Shield PvE hit (`DecreaseDefenseItemDurabilityAsync`): shield factor `5`, ELF (`ArcheryMinDmg > 0`) `2`, DL (`TotalLeadership > 0`) `6`, MG (`SkillMultiplier > 1 && WizardryAttackDamageIncrease > 0`) `7`, others `3`.
  - Pet (`DecreasePetDurabilityAsync`): `damageFactor` `3` (Angel/Rudolf), `2` (Imp/DarkHorse/Fenrir), `10` (Dinorant), `1.5` (Demon), `1` (other); DarkHorse `decrement = (1 + dmg*2 / DamagePerOnePetDurability) / (1500 + TrainablePetDurationIncrease)` and drops to 90% PET EXP (clamped to level min) on `0` durability rather than destroying; Fenrir divides by `160` (Archery) or `200` (others).
  - Weapon (`DecreaseWeaponDurabilityAfterHitAsync`): magic weapons (`1.33 * StaffRise/BookRise`, `defenseFactor = 1`, `hits * 1056/564`), physical (`1.5 * attr`, `defenseFactor = 2`, archery `hits * 780/564`); Raven `dmg * 0.02 / (hits * 800/564 + TrainablePetDurationIncrease)`.
- **Outlaw kills (`Player.AfterKilledPlayerAsync`)**:
  - Exempts: Duel opponent, killed `State >= PlayerKiller1stStage`, `SelfDefense`, `IPlayerKillPenaltyExemptionPlugIn`, rival guild hostility (`AreGuildsRival`).
  - Hero-state timer: `HeroStateDuration = 1h` per hero stage step; Outlaw uses `OutlawPoints` and limits legacy saved remaining seconds in `LimitHeroStateRemainingTimeAsync`.
- **Movement (`PlayerMovement.cs`)**:
  - `WalkMovementSpeed = 12.0`, `referenceFrameTimeMilliseconds = 40.0`, `terrainScale = 100.0`, `NextTileDistance = 1.5`, `MaxUnchargedCorrectionDistance = 3.0`, `UnchargedCorrectionRecoveryFactor = 0.5`.
- **Experience (`PlayerExperience.cs`)**:
  - `TryGetExperienceKind` gates Master EXP on `characterClass.IsMasterClass && currentLevel >= CatchupCapPlugIn.GetEffectiveMaximumLevel && currentLevel >= MaximumLevel && SeasonPlanPlugIn.IsFeatureUnlocked(MasterLevels)`.
  - `AddRewardExperienceAsync(baseExperience)`: scales reward by `MasterExperienceRate` or `ExperienceRate` (falling back to `1` if `<= 0`).
- **Persistence (`PlayerPersistence.cs`)**:
  - Uses per-player `AsyncLock` + `AsyncLocal<bool> _lockHeld` (re-entrant per async flow) and calls `SeasonSharedAccounts.SyncCoinsAsync` during `SaveProgressAsync`.
