# Crywolf S6 vs S9 Reference

Derived from legacy Season 6 and Season 9 `IGC_CryWolf.xml`. Both seasons define identical mechanics, cycle timings, monster scores, ranks, benefits, and penalties.

## 1. Cycle State Timings (`CycleSettings`)
| State ID | Name | Duration |
|---|---|---|
| `1` | `CRYWOLF_STATE_NOTIFY_1` | `600s` (10 min) |
| `2` | `CRYWOLF_STATE_NOTIFY_2` | `300s` (5 min) |
| `3` | `CRYWOLF_STATE_READY` | `150s` |
| `4` | `CRYWOLF_STATE_START` | `900s` (15 min battle) |
| `5` | `CRYWOLF_STATE_END` | `15s` |
| `6` | `CRYWOLF_STATE_ENDCYCLE` | `10s` |

## 2. Monster Groups & Timings (`MonsterGroupSettings`)
- **Army AI Change Delay** (`MonsterAIChangeTime`): `300s` (5 min after battle start, army attacks statue)
- **Balgass Spawn Delay** (`BalgassTurnUpTime`): `600s` (10 min after battle start)
- **Balgass AI Group**: `5`
- **Monster AI Groups**: `2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13`

## 3. MVP Scoring & Rank Experience (`MvP_*`)
- **Kill / Contract Scores**:
  - `Balgass`: `7000`
  - `Contractor` (active Elf at end): `6000`
  - `DarkElf`: `3000`
  - `Tanker`: `1000`
  - `Soram`: `700`
  - `Balram`: `600`
  - `DeathSpirit`: `600`
- **Rank Thresholds & EXP Rewards**:
  - `Rank D`: min score `0`, EXP `0`
  - `Rank C`: min score `1001`, EXP `200,000`
  - `Rank B`: min score `3001`, EXP `800,000`
  - `Rank A`: min score `5001`, EXP `1,200,000`
  - `Rank S`: min score `10001`, EXP `1,800,000`

## 4. Occupation Benefits & Penalties
- **Peace (Defended) Benefits** (`Enable="1"`):
  - `PlusChaosRate`: `+5%` Chaos Machine ticket mix bonus
  - `MonsterHPRate`: `90%` monster maximum HP
  - `KundunHPRefill`: `0` (disabled by default)
- **Occupied (Failed) Penalties** (`Enable="0"` by default):
  - `GemDropPenaltyRate`: `70`
  - `GettingExpPenaltyRate`: `100`
