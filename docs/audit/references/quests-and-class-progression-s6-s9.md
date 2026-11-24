# Reference: Class Quests, Episode Quest Items & Buffer NPC Rules (S6 & S9 Suites vs HNMO)

> Sanitized reference captured during the `server/src/GameLogic/PlayerActions/Quests` audit (2026-11-24) so future checks do not need to re-read these suite files.

## 1. Legacy Class Quest Progression (S6 & S9 `ClassQuest.xml`)
Both Season 6 and Season 9 legacy suites define the same 7 class progression quests (S9 adds Grow Lancer eligibility on Index 4–6):

| Index | Name | NPC | Min Lv | Zen Cost | Prereq (`Condition.Index`) | Requirement(s) | Reward |
|---|---|---|---|---|---|---|---|
| `0` | Find the Scroll of Emperor | `235` | `150` | `1,000,000` | None (excluded for MG/DL/RF/GL via `MinLevel=10000`) | `14:23` lv0 x1 (monster lv 45–60, rate `10`) | `+10` stat points |
| `1` | Four Treasures | `235` | `150` (via req `0`) | `2,000,000` | Quest `0` | DK `14:24`, FE `14:25`, DW `14:26`, Sum `14:68` (monster lv 62–76, rate `10`) | `+10` stat points + 2nd Class Evolution |
| `2` | Gain Hero Status | `229` | `220` | `3,000,000` | Quest `1` | `14:23` lv1 (`Ring of Honor`, monster lv 72–108, rate `10`) | `+1` point per level from Lv220 (+ retroactive points) |
| `3` | Secret of Dark Stone | `229` | `220` | `2,000,000` | Quest `2` (BK only) | `14:24` lv1 (`Dark Stone`, monster lv 72–108, rate `10`) | Combo Skill unlock |
| `4` | Certificate of Strength | `406` | `380` | `5,000,000` | Quest `2` (DW/FE/Sum), Quest `3` (DK), None (MG/DL/RF/GL) | `14:65` + `14:66` + `14:67` (boss drop rate `1000`) | `+10` stat points |
| `5` | Infiltration of Barrack | `406` | `400` | `7,000,000` | Quest `4` | Kill `20`x Balram (`409`), `20`x Death Spirit (`410`), `20`x Soram (`411`) | `+20` stat points |
| `6` | Infiltration of Refuge | `406` | `400` | `10,000,000` | Quest `5` | Kill `1`x Dark Elf (`412`) | `+30` stat points + 3rd Class Evolution |

## 2. Episode Quest Item Flags (`Quest_Define.lua` & `Quest_Info.lua`)
Bitmask flags for quest item restrictions (`EN_OVERLAP=0x01`, `EN_DROP=0x02`, `EN_WAREHOUSE=0x04`, `EN_SELL_SHOP=0x08`, `EN_PERSONAL_SHOP=0x10`, `EN_PERSONAL_TRADE=0x20`, `EN_QUEST_PROG=0x40`):
- **Allowed Sell to NPC + Overlap (`0x09`)**: `13:148`..`13:151` (Ep9–11 inks/parchment), `14:152` & `14:171` (mission confirmations), `14:153`..`14:156` (Stardust, Kult Stone, Tantalose Armor, Ash Slaughterer Club).
- **Allowed Sell to NPC only (`0x08`, no overlap)**: `14:164`..`14:166`.
- **Progress Item only (`0x40`)**: `14:151` & `14:170` (Ep20 commission letters).
- **Episode Teleport Targets** (`Index: Map (X, Y), Dir`): `0: Map 51 (55, 199) d2`, `1: Map 0 (131, 138) d2`, `2: Map 3 (186, 109) d2`, `3: Map 2 (226, 52) d2`.
- **Episode Quest NPC IDs**: `257` Shadow Phantom, `543` Gens Duprian, `544` Gens Vanert, `566` Tersia, `567` Vaina, `568` Zairo, `581` Derubish.
- **Reward Item Optional Parameters**: `ExOpt1..ExOpt6` (`0x20..0x01`), `Opt4/8/12/16` (`1..4`).

## 3. Buffer NPC (`CommonServer.cfg`)
- `ShadowPhantomMaxLevel = 220`: Soldier buffer NPC stops buffing characters above level `220` in both S6 and S9 defaults.
- `QuestNPCTeleportTime = 9000` (seconds).
