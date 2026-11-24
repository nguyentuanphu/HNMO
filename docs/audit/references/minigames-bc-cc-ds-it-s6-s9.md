# Reference: Mini-Games (Blood Castle, Chaos Castle, Devil Square, Illusion Temple) (S6 & S9 Suites vs HNMO)

> Sanitized reference captured during the `server/src/GameLogic/MiniGames (files 1)` audit (2026-11-24) so future checks do not need to re-read these suite files.

## 1. Blood Castle (`the suite's Blood Castle config file` S6 / `the suite's Blood Castle config file` S9)
- **Timings (S9)**: Enter `5 min`, Play `15 min`, Exit `1 min`; schedule every `2h` at `:30`.
- **Early finish guard**: `EarlyFinishRestriction = 0`, `EarlyFinishMinTime = 300s` (disabled by default in both S6 & S9).
- **Ticket drop rates (`n/10000`)**: S6 `Scroll of Archangel (13:16) = 80`, `Blood Bone (13:17) = 80`; S9 `800 / 800`.
- **Gate & Statue HP per Castle Level (S9)**:
  - BC1 (`Level 0`): Statue `65,000` / Gate `150,000`
  - BC2 (`Level 1`): Statue `105,000` / Gate `205,000`
  - BC3 (`Level 2`): Statue `145,000` / Gate `260,000`
  - BC4 (`Level 3`): Statue `185,000` / Gate `325,000`
  - BC5 (`Level 4`): Statue `225,000` / Gate `400,000`
  - BC6 (`Level 5`): Statue `265,000` / Gate `480,000`
  - BC7 (`Level 6`): Statue `305,000` / Gate `565,000`
  - BC8 (`Level 7`): Statue `345,000` / Gate `665,000`
- **HNMO rules**: Castle Gate `131`, Statue of Saint `132`, Archangel Quest Item `(13,19)`, drops item on carrier disconnect mid-event; supports `ExperiencePerRemainingSeconds`.

## 2. Chaos Castle (`the suite's Chaos Castle config file` S6 / `the suite's Chaos Castle config file` S9)
- **Min players**: `2` in S6 (`ChaosCastleStartUser = 2`) and HNMO (`DefaultMinimumPlayerCount = 2`); `1` in S9 XML. Max objects = `100`.
- **Timings (S9)**: Enter `5 min`, Duration `10 min`, Exit `1 min`.
- **Monster jewel drops per level (`ChaosCastleDropGenerator`)**:
  - CC1: `0B, 2S`; CC2: `1B, 1S`; CC3: `1B, 2S`; CC4: `1B, 2S`; CC5: `2B, 1S`; CC6: `2B, 2S`; CC7: `2B, 3S`.
- **HNMO rules**: Score `2` pts per monster kill, `1` pt per player kill; `50%` monster death explosion knockback (`BlowOutDistance` 0..4 by distance <= 3, no extra explosion damage applied, falls off bridge -> instant kill). Disallows Uniria (`13:2`), Dinorant (`13:3`), Fenrir (`13:37`), transformation rings, monster summons, and buffing other players.

## 3. Devil Square (`the suite's Devil Square config file` S6)
- `DevilSquareMaxPlayerPerRoom = 15`, `EyesOfDevilSquareDropRate = 80/10000`, `KeyOfDevilSquareDropRate = 80/10000`.
- HNMO awards `GameLevel` score per monster kill; no Zen drop (`todo add money -> in original servers DS drops no money`).

## 4. Illusion Temple Classic (`the suite's Illusion Temple Classic config file` & `.xml` S6)
- **Min players**: `2`; **Ticket drops**: Covenant (`13:50`) `10/10000`, Old Scroll (`13:49`) `10/10000`.
- **Timings**: Enter announce `5m`, WaitingArea `1m`, Duration `15m`, TownSpawn `4m`, Statue respawn `5s` (`StatueRespawnDelay = 5s`).
- **Match & Scoring**:
  - `MaxMatchPoint = 7` (`WinningScore = 7`), minimum winning score `2` (`1:0` = draw in HNMO `IllusionTempleScore.cs`).
  - NPCs/Items: Stone Statue `380`, Team Storages `383` (Allied Forces) / `384` (Illusion Forces), Sacred Relic (`14:64`), Arena Monsters `386..399`.
  - Skill Points: Start `10`, Max `90`; S6 `OpponentKill = 5`, `MonsterKill = 2` (HNMO `SkillPointsPerPlayerKill = 1`, `SkillPointsPerMonsterKill = 2`); `MinUseSkillCount = 3` (S6).
  - Special Skills (`210..213`): Order of Protection `5 SP`, Restraint `5 SP` (range 6), Tracking `10 SP`, Weaken `5 SP` (range 6).
- **S6 Golden Fenrir Upgrade Bonus** (`MinParticipants = 6`, `GoldenFenrirChangeUp = 1`): Red `10%`, Blue `20%`, Black `30%`.
