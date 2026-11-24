# Audit Findings — `server/src/GameLogic/PlugIns/MossMerchant`

- **Unit**: `server/src/GameLogic/PlugIns/MossMerchant` (3 files, 316 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `MossGambleCatalog.cs`, `MossGambleConfiguration.cs`, `MossGamblePlugIn.cs`

## Rules & Cross-Check
| Rule | HNMO Implementation | Season 6 / 9 Suite Reference | Status |
| --- | --- | --- | --- |
| Reward pools & tier weights (`13:71..75`) | `MossGambleCatalog.cs:15-58`: identical 5 categories, 5 tiers (`560k / 380k / 50k / 9,980 / 20`), identical Lv `0..3` ranges, Skill/Luck/Option/Exc per-million rates, `+4/+8/+12` (`85%/10%/5%`), and `90%` 2-exc / `10%` 3-exc | Suite `GamblingLottery.xml` (identical across S6 & S9) | Same |
| Gamble Zen price | `MossGambleConfiguration.cs:22`: `PriceZen = 2_000_000` | Suite `GamblingLottery.xml:80`: `GamblingPrice = 1000000` | Differs (`2,000,000` vs `1,000,000`) |
| Exc option bit distribution | `MossGambleCatalog.cs:174`: uniform random over `excPool` | Suite `GamblingLottery.xml:121-128`: weighted `10% / 20% / 20% / 20% / 20% / 10%` | Differs (minor) |

## Observations
- Clean implementation; voucher purchase replaces the voucher with the rolled `TemporaryItem` before delivery.
