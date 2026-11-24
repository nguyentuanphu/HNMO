# Reference: Gens System Rules, Ranks, Contributions & Rewards (S6 & S9 Suites vs HNMO)

> Sanitized reference captured during the `server/src/GameLogic/Gens` and `PlayerActions/Gens` audit (2026-11-24) so future checks do not need to re-read these suite files.

## 1. Core Settings (S6 `IGC_GensSystem.ini` & S9 `IGC_GensSystem.xml`)
- **Families & NPCs**: Duprian (`543`), Vanert (`544`); Rank values `1` (Grand Duke) to `14` (Private); initial contribution on join = `10`.
- **Chat & Social Restrictions** (S6 & S9 defaults):
  - `CanSeeOppositeGensChat = 0`
  - `CanJoinPartyWhileOppositeGens = 0`
  - `CanJoinGuildWhileOppositeGens = 0`
  - `CanJoinUnionWhileOppositeGens = 0`
  - `CanCreatePartyOnBattleZone = 0`
- **Battle Zone Config**:
  - S6: `GensBattlezonesFromMuBlue = 0` (when `1`: Battle Map = Vulcanus `63`); S9 enables BattleZone Map `63` (Vulcanus, `Warp Index 42`, `FamilyAccessType = 0` for both families), with optional disabled entries for Maps `7` (Atlans), `33` (Aida), `37` (Kanturu), `56` (Swamp of Calmness).
  - `PkPenaltyAddNeedZenMapMove = 2` (2x warp fee to Battle Zone while PK).
  - `BattleZoneAddExperience = 1.0`, `ML_BattleZoneAddExperience = 1.0`, `DamageReduction = 100` (S9).
  - S9 `BlueMuMode = 0` (when `1`: PK death drops item 100%, loses 3% Zen, applies 5-min debuff `106`).
- **Anti-Abuse Kill Counting**:
  - `AbusingPenaltyWarning = 3` kills of same victim
  - `AbusingPenaltyLimit = 6` kills of same victim (no more contribution points)
  - `AbusingResetTime = 60` minutes
- **Reward Window**: `StartDay = 1`, `EndDay = 7`, `MinRankToGainReward = 300`.

## 2. Ranking Thresholds & Monthly Rewards (`IGC_GensRanking.dat` / S9 XML vs HNMO)
| Rank | Title | S6/S9 `ReqContributePoint` | S6/S9 `RankingPosition` | HNMO `MinimumContribution` | Monthly Reward Item (`14:141..144`) | Count |
|---|---|---|---|---|---|---|
| `1` | Grand Duke | `0` | `1` | `10,000` | Shinning Jewellery Case (`14:141`) | `30` |
| `2` | Duke | `0` | `5` | `10,000` | Shinning Jewellery Case (`14:141`) | `20` |
| `3` | Marquis | `0` | `10` | `10,000` | Elegant Jewellery Case (`14:142`) | `20` |
| `4` | Count | `0` | `30` | `10,000` | Elegant Jewellery Case (`14:142`) | `10` |
| `5` | Viscount | `0` | `50` | `10,000` | Steel Jewellery Case (`14:143`) | `10` |
| `6` | Baron | `0` | `100` | `10,000` | Steel Jewellery Case (`14:143`) | `5` |
| `7` | Knight Commander | `0` | `200` | `10,000` | Old Jewellery Case (`14:144`) | `5` |
| `8` | Superior Knight | `0` | `300` | `10,000` | Old Jewellery Case (`14:144`) | `3` |
| `9` | Knight | `10,000` | `0` | `10,000` | — | `0` |
| `10` | Guard Perfect | `6,000` | `0` | `6,000` | — | `0` |
| `11` | Officer | `3,000` | `0` | `3,000` | — | `0` |
| `12` | Lieutenant | `1,500` | `0` | `1,500` | — | `0` |
| `13` | Sergeant | `500` | `0` | `500` | — | `0` |
| `14` | Private | `0` | `0` | `0` | — | `0` |

## 3. HNMO `GensConfiguration` Kill Contribution & Rank Bonus Tables
- **`KillContributions`** (level diff = killer - victim):
  - `< -50`: `+7 / -3`; `[-50, -11]`: `+6 / -3`; `[-10, 10]`: `+5 / -3`; `[11, 30]`: `+3 / -3`; `[31, 50]`: `+2 / -1`; `>= 51`: `+1 / -1`.
- **`RankBonuses`** (rank diff = killerRank - victimRank, when killerRank >= victimRank + 1; uses `LowRankBonus` when `killerRank >= 9`):
  - `>= 1`: `+3` (low `+1`); `>= 2`: `+4` (low `+2`); `>= 3`: `+5` (low `+3`).
- `MinimumVictimContribution = 1`.
