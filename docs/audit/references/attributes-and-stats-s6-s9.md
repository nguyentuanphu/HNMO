# Reference: Character Stat Formulas, Shield System & Combat Caps (S6 & S9 Suites vs HNMO)

> Sanitized reference captured during the `server/src/GameLogic/Attributes` audit (2026-11-24) so future checks do not need to re-read these suite files.

## 1. Base Damage, Defense, Speed & Success Rate Formulas (S6 & S9 Character Calculation Script)

### Physical / Fist Damage
| Class | Min Damage | Max Damage | Notes |
|---|---|---|---|
| Wizard (SM/GM) | `Str / 8` | `Str / 4` | Fist |
| Knight (BK/BM) | `Str / 6` | `Str / 4` | Fist |
| Elf (no bow) | `(Dex + Str) / 7` | `(Dex + Str) / 4` | Fist |
| Elf (with bow) | `Str / 14 + Dex / 7` | `Str / 8 + Dex / 4` | Bow/Crossbow |
| Magic Gladiator (DM) | `Str / 6 + Ene / 12` | `Str / 4 + Ene / 8` | Fist |
| Dark Lord (LE) | `Str / 7 + Ene / 14` | `Str / 5 + Ene / 10` | Fist |
| Summoner (BS/DimM) | `(Dex + Str) / 7` | `(Dex + Str) / 4` | Fist |
| Rage Fighter (FM) | `Str / 7 + Vit / 15` | `Str / 5 + Vit / 12` | Fist |
| Grow Lancer (ML, **S9 only**) | `Str / 8 + Dex / 10` | `Str / 4 + Dex / 6` | Fist |

### Magic & Curse Damage
- All classes (S6 & S9): `MagicDamageMin = Ene / 9`, `MagicDamageMax = Ene / 4`.
- Summoner (S6 & S9): `MagicDamageMax = Ene / 4 + 0.015`, `CurseDamageMin = Ene / 9`, `CurseDamageMax = Ene / 4 + 0.015`.

### Dual Wield & Pet Bonus Formulas
- **Two Same Weapons** (DK, MG; plus DL in S6): `65%` of each hand (`* 65 / 100`).
- **Two Different Weapons** (DK, MG, DL): `55%` of each hand (`* 55 / 100`).
- **Rage Fighter Two Weapons**: Min `AtkMin * 120 / 100 / 2` (`60%` per hand), Max `AtkMax * 130 / 100 / 2` (`65%` per hand).
- **Dark Horse Defense Bonus**: `Dex / 20 + 5 + PetLevel * 2`.
- **Dark Lord Pet Multipliers** (`CalcCharacter.ini`): `DarkHorseDamageMultiplier = 100.0`, `DarkRavenDamageMultiplier = 100.0`.

### Attack Speed, Defense & Rates
| Class | Attack Speed | Magic Speed | Defense (General) | Def Rate PvM | Atk Rate PvM | Atk Rate PvP | Def Rate PvP |
|---|---|---|---|---|---|---|---|
| Wizard | `Dex / 20` | `Dex / 10` | `Dex / 4` | `Dex / 3` | `5*Lvl + 1.5*Dex + Str/4` | `4*Dex + 3*Lvl` | `Dex/4 + 2*Lvl` |
| Knight | `Dex / 15` | `Dex / 20` | `Dex / 3` | `Dex / 3` | `5*Lvl + 1.5*Dex + Str/4` | `4.5*Dex + 3*Lvl` | `Dex/2 + 2*Lvl` |
| Elf | `Dex / 50` | `Dex / 50` | `Dex / 10` | `Dex / 4` | `5*Lvl + 1.5*Dex + Str/4` | `0.6*Dex + 3*Lvl` | `Dex/10 + 2*Lvl` |
| Gladiator | `Dex / 15` | `Dex / 20` | `Dex / 4` | `Dex / 3` | `5*Lvl + 1.5*Dex + Str/4` | `3.5*Dex + 3*Lvl` | `Dex/4 + 2*Lvl` |
| Dark Lord | `Dex / 10` | `Dex / 10` | `Dex / 7` | `Dex / 7` | `5*Lvl + 3*Dex + Str/4 + Cmd/10` | `4*Dex + 3*Lvl` | `Dex/2 + 2*Lvl` |
| Summoner | `Dex / 20` | `Dex / 20` | `Dex / 3` | `Dex / 4` | `5*Lvl + 1.5*Dex + Str/4` | `3.5*Dex + 3*Lvl` | `Dex/2 + 2*Lvl` |
| Rage Fighter | `Dex / 9` | `Dex / 9` | `Dex / 8` | `Dex / 10` | `3*Lvl + 1.25*Dex + Str/6` | `3.6*Dex + 2.6*Lvl` | `Dex/5 + 1.5*Lvl` |
| Grow Lancer (S9) | `Dex / 20` | `Dex / 20` | `Dex / 7` | `Dex / 4` | `5*Lvl + 1.25*Dex + Str/4` | `2*Dex + 3*Lvl` | `Dex/5 + 2*Lvl` |

### Elemental Damage & Defense (PvP & PvM)
- **Elemental Defense**: Same as general Defense except Gladiator uses `Dex / 5` (S9 Grow Lancer `Dex / 7`).
- **Elemental Damage**:
  - Wizard: `ItemMin + Ene/9` .. `ItemMax + Ene/6`
  - Knight: `ItemMin + Str/6` .. `ItemMax + Str/4`
  - Elf: `ItemMin + Dex/8 + Str/14` .. `ItemMax + Dex/4 + Str/8`
  - Gladiator: `ItemMin + Str/10 + Ene/14` .. `ItemMax + Str/6 + Ene/8`
  - Dark Lord: `ItemMin + Str/10 + Ene/14` .. `ItemMax + Str/6 + Ene/10`
  - Summoner: `ItemMin + Ene/10` .. `ItemMax + Ene/6`
  - Rage Fighter: `ItemMin + Str/10 + Vit/20` .. `ItemMax + Str/6 + Vit/15`
  - Grow Lancer (S9): `ItemMin + Str/4 + Dex` .. `ItemMax + Str/4 + Dex`

## 2. Shield Gage (SD) & Combat Caps (S6 & S9 Server Configuration)
- **Shield System**: `ShieldSystemOn = 1`, `DamageDevideToSD = 90`, `DamageDevideToHP = 10` (90% SD / 10% HP split).
- **Shield Formula Constants**: `ShieldGageConstA = 12`, `ShieldGageConstB = 30`, `ConstNumberOfShieldPoint = 20`, `ShieldAutoRefillOn = 1`, `ShieldAutoRefilOnSafeZone = 1`, `ShieldComboMissOptionOn = 0`.
- **PvP Hit Chance Formula**:
  - `ExpressionA = (AttackRate * 10000.0f) / (AttackRate + DefenseRate)`
  - `ExpressionB = (AttackLevel * 10000) / (AttackLevel + DefenseLevel)`
  - `SuccessAttackRateOption = 20000`
- **SD Potions Drop & Chaos Mix**:
  - Compound Potion Lv1 (Small SD): drop `100/10000` at min monster level `68`; mix `50%`, `100,000` Zen.
  - Compound Potion Lv2 (Medium SD): drop `80/10000` at min monster level `96`; mix `30%`, `500,000` Zen.
  - Compound Potion Lv3 (Large SD): drop `50/10000` at min monster level `118`; mix `30%`, `1,000,000` Zen.
- **Global Combat Caps** (`CalcCharacter.ini`):
  - `MaxDamageReflectPercent = 100`
  - `MaximumDamageDec = 100`
  - `ElementalDmgAffectSDOnly = 1`
- **Attribute Caps in HNMO (`Stats.cs`)**:
  - `AttackSpeed.MaximumValue = 200`, `MagicSpeed.MaximumValue = 200`.
  - `AreTwoWeaponsEquipped.MaximumValue = 1`, `IsOneHandedSwordEquipped.MaximumValue = 1`, `IsOneHandedStaffEquipped.MaximumValue = 1`.
  - `SoulBarrierReceiveDecrement.MaximumValue = 0.7f` (70% cap).
  - `ShieldRecoveryRampFactor.MaximumValue = 3` (original range [2, 3]).
  - `DragonSlasherShieldDamageRate.MaximumValue = 1`, `ShieldRatio.MaximumValue = 1`.
