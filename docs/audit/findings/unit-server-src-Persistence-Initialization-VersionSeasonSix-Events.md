# Audit Findings: `server/src/Persistence/Initialization/VersionSeasonSix/Events`

- **Unit**: `server/src/Persistence/Initialization/VersionSeasonSix/Events` (15 files, 2,453 lines)
- **Checked on**: 2026-11-24
- **Checked at**: `20de6828`
- **Status**: `Checked`

## Comparison against Season 6 / Season 9 Suites

All 15 Season 6 event initializer and spawn files were audited against the Season 6 and Season 9 suites' Blood Castle, Chaos Castle, Devil Square, Illusion Temple, Doppelganger, Imperial Guardian, Crywolf, Castle Siege, Kanturu, monster list, and move list configuration files (via direct inspection and the sanitized references in `docs/audit/references/`).

## File-by-File Findings

- `BloodCastleInitializer.cs:212`: Configures Blood Castle Enter `5m`, Game `15m`, Exit `1m`, and max `10` players -> timings **same** as the Season 9 suite's Blood Castle config file (`Enter=5 min`, `Play=15 min`, `Exit=1 min`).
- `ChaosCastleInitializer.cs:90-93`: Configures Chaos Castle Game `10m`, min `2` players, max `70` players -> Game duration `10m` and min `2` players **same** as the Season 6 suite's Chaos Castle config file (`ChaosCastleStartUser=2`); **differs** from the Season 9 suite's Chaos Castle config file (`min=1`) and max objects (`100` in suite).
- `DevilSquareInitializer.cs:17`: Configures Devil Square max `15` players -> **same** as the Season 6 suite's Devil Square config file (`DevilSquareMaxPlayerPerRoom=15`).
- `IllusionTempleInitializer.cs:89-93,128,134,144`: Configures Enter `5m`, Game `15m`, min `2`, max `10`, `Protection`/`Restraint` duration `15s`, and `50%` damage reduction -> min `2` and Game `15m` **same** as the Season 6 suite's Illusion Temple Classic config file; team storage/relic setup and skill rules align with Illusion Temple Classic.
- `DoppelgangerInitializer.cs:51-54` & `DoppelgangerMonsters.cs:49-115`:
  - `DoppelgangerInitializer.cs:51-54`: Enter `30s`, Game `10m`, Exit `1m`, max `5` players, `1` per party -> Play `10m` and End `1m` **same** as the Season 6 suite's Doppelganger Lua script (`LuaSetPlayTime(10)`, `LuaSetEndTime(1)`), whereas Enter `30s` **differs** from `LuaSetReadyTime(1)` (`1m` in the Season 6 suite's Doppelganger script).
  - `DoppelgangerMonsters.cs:49-115`: Defines monsters `#529..539` and chests `#541`/`#542` with fixed reward drop chances (Jewels `100%`/`90%`, Feather `8%`, Crest `2%`) -> **differs** from the Season 6/9 suites' Doppelganger Lua/drop-info configuration, which scales monster HP/level/damage dynamically by player level and participant count and uses section-based drop rates (`SkillRate`/`LuckRate`/`ExcItemRate`).
- `ImperialGuardianInitializer.cs:63-104`, `ImperialGuardianDay.cs`, `ImperialGuardianSpawn.cs`, `ImperialGuardianSpawns.cs:19-467`:
  - `ImperialGuardianInitializer.cs:63-104`: Maps Monday–Sunday waves across Fortresses 1–4, Lv `150-400`, Enter `60s`, Game `50m`, Exit `1m`, max `5`, Jerint `#522` -> **differs** from the Season 6 and Season 9 suites' Imperial Guardian config files (`WaitPlayerTime=180s` standby per stage and `TimeAttackTime=600s` `10m` round timer per zone rather than a single `Enter=60s` / `Game=50m` timeout).
  - `ImperialGuardianSpawn.cs:20-25` & `ImperialGuardianSpawns.cs:19-467`: Maps day-of-week + zone to `WaveNumber` across maps 69–72 (`FortressOfImperialGuardian1..4`), adjusting direction (`+1`) for HNMO's `Direction` enum.
- `KanturuInitializer.cs:34-41`: Configures Kanturu Game `135m`, Lv `350-400`, max `15` -> sub-state cycle limits follow `docs/audit/references/kanturu-s6-s9.md` (`BattleStandby` + 3 Maya waves + Nightmare).
- `CrywolfInitializer.cs:20-236`, `CrywolfSpawn.cs:15`, `CrywolfSpawns.cs:19-97`:
  - `CrywolfInitializer.cs:58-66`: Creates monsters `340` Dark Elf (Lv `135`, HP `1,500,000`, Damage `800-900`, Defense `900`, AttackRate `1500`, DefenseRate `370`), `341` Soram (Lv `134`, HP `100,000`), `344` Balram (`90,000`), `345` DeathSpirit (`95,000`), `348` Tanker (`450,000`), `349` Balgass (`500,000`) -> stats **same** as the Season 6/9 suites' monster list files (`340` and `341` verified verbatim against S6/S9 monster list XML).
  - `CrywolfInitializer.cs:159-174`: Adds Crywolf warp at `Index = 26`, `Costs = 10000`, `LevelRequirement = 300` -> `LevelRequirement = 300` **same** as the Season 6 suite's move list file (`MinLevel="300"`), while warp `Index` (`26` vs `48`) and Zen cost (`10,000` vs `15,000`) **differ**.
  - `CrywolfSpawns.cs:19-97`: Defines waves `1..13` for Balgass (`349`, group `5`), Dark Elf leaders (`340`), and army groups -> army group structure **same** as the Season 6/9 suites' Crywolf config files.
- `CastleSiegeInitializer.cs:17-340`:
  - `CastleSiegeInitializer.cs:58-85,214-243,245-271`: `GateBuyPrice = 9_500_000`, `StatueBuyPrice = 4_500_000`, `MaxAttackingGuilds = 3`, `CrownHoldTimeSeconds = 30`, `RegisterMinLevel = 200`, `RegisterMinMembers = 20`; Gate (`277`) defense `100/180/300/520`, Gate HP `1.9M/2.5M/3.5M/5.2M`, Statue (`283`) defense `80/180/340/550`, HP `1.5M/2.2M/3.4M/5.0M`, regen `0/1/2/3`, and JoG/Zen costs -> **same** upgrade tables and coordinates for Gates (`277` x6), Statues (`283` x4), and Catapults (`221` x2, `222` x2) as the Season 6/9 suites' Castle Siege config files.
  - `CastleSiegeInitializer.cs:200-212`: `InitializeStateSchedule` configures a 7-day weekly schedule (Saturday `Ready=18:00`, `Start=20:00`, `End=22:00`, `EndCycle=22:05`) -> battle hour (`20:00-22:00`) **same** as the Season 6 suite's sample timetable (S9 sample is `08:00-10:00`), whereas the 7-day period **differs** from the 14-day (`Days="14"`) cycle in the Season 6/9 suites' Castle Siege config files; `InitializeNpcDefinitions` also uses monster number `219` (x6) for towers rather than `288` (x9) and omits Shield (`215`).
