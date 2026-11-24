# Reference: Jewels, Harmony/Refine Stones & Potion Settings (S6 & S9 Suites vs HNMO)

> Sanitized reference captured during the `server/src/GameLogic/PlayerActions/ItemConsumeActions` audit (2026-11-24) so future checks do not need to re-read these suite files.

## 1. Jewel Upgrade Rates (`the suite's common settings file` & `CommonServer.cfg` — S6 & S9)
| Jewel / Item | S6/S9 Setting | Value | HNMO Default | Notes |
|---|---|---|---|---|
| **Jewel of Bless** | Level <= +6 | `100%` | `100%` (up to `MaximumLevel = 5` -> `+6`) | Matches S6/S9; supports configurable `RepairTargetItems` (empty by default) |
| **Jewel of Soul** | `UseSoulRate` / `UseSoulWithLuckRate` | `50%` / `75%` (`+25%` luck) | `50%` / `75%`, resets to `+0` on fail `>= +7` | Matches S6/S9 (`MaximumLevel = 8` -> `+9`) |
| **Jewel of Life** | `UseLifeRate` | `50%` | `50%`, `ItemFailResult.RemoveOption` | Matches base rate; HNMO removes option on fail |
| **Jewel of Harmony** | `StrengthenSuccessRate` (S6 `CommonServer.cfg:250`) | `60%` | `60%`, `ItemFailResult.None`, excludes Ancient | Matches S6 |
| **Lower Refine Stone** | `SmeltingItemSuccessRateNor` (S6:251) | `20%` | `20%`, `SetOptionToBaseLevel` on fail | Matches S6 |
| **Higher Refine Stone** | `SmeltingItemSuccessRateExt` (S6:252) | `80%` | `80%`, `SetOptionToBaseLevel` on fail | Matches S6 |
| **Lucky Items** | `AllowEnchantLuckyItemByJewels` | `0` (disallowed) | Not blocked in `UpgradeItemLevelJewelConsumeHandlerPlugIn` unless `DisallowedItems` | Check `DisallowedItems` config |

## 2. Potion Delay & Recovery Settings (`the suite's common settings file` vs HNMO)
- **Potion cooldown**:
  - S6 & S9 `the suite's common settings file`: `PotionDelayTime = 100` ms (`0.1s`).
  - HNMO `RecoverConsumeHandlerPlugIn` (`ManaHealth`, `ShieldPotion`, `ElitePotion`, `ComplexPotion`): `CooldownTime = 500 ms` (`0.5s`).
- **HNMO Potion Recovery Defaults**:
  - Apple / Health / Mana (`Multiplier` = `0 / 1 / 2 / 3`): `TotalRecoverPercentage = Multiplier * 10%` (`0%, 10%, 20%, 30%`), `AdditionalRecoverMinusCharacterLevel = (Multiplier + 1) * 50` (`50, 100, 150, 200`), `+1%` per item level, step delays `200ms (20%) + 600ms (60%) + 200ms (20%)` scaled by `1 - item.Level / 16.0`.
  - Shield Potions (Small/Med/Large): `20% / 40% / 100%`, same 3-step curve and `0.5s` cooldown.
  - Complex Potions (Small/Med/Large): apply both Health + Shield handlers on the same `PotionCooldownUntil`.
  - Elite Potions: instant `100%` (HP/MP), `65%` (Medium Elite HP / Elite SD), `0.5s` cooldown.
- **HNMO Reset Fruits**: `PointsPerDurability = 10`, returns clamped `[0, min(investedPoints, ushort.MaxValue)]` to `LevelUpPoints`, blocks if any item is equipped, consumes full durability (`0`).
- **HNMO Kalima Ticket Min Levels**: Normal `[40, 131, 181, 231, 281, 331, 350]`; Special (MG/DL/RF) `[20, 111, 161, 211, 261, 311, 350]`.
- **HNMO Siege Potions (`SiegePotion`)**: level `0` -> effect `10` (Bless), level `1` -> effect `11` (Soul).
