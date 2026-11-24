# Parser Findings: Events & Invasions (`diff-events-all.txt`)

- **Checked on**: 2026-11-24
- **Outputs**: [diff-events-all.txt](../data/diff-events-all.txt) (95 lines), [s9/diff-events-all.txt](../data/s9/diff-events-all.txt) (95 lines)
- **Sources**: all 16 Season 6 event/invasion configs and all 22 Season 9 event/invasion configs compared against `docs/audit/data/hnmo/events.csv`, `docs/audit/data/hnmo-s9/events.csv`, `VersionSeasonSix/Events/`, `InvasionMobsInitialization.cs`, and `AddRaklionEventUpdatePlugIn.cs`

## Fix Summary

`parseSuiteEventsAndInvasions` previously recorded only filenames and format tags (`BloodCastle.ini (ini) | BloodCastle.xml (xml)`). The updated parser extracts `enable`, `minLevel`, `min/maxPlayers`, `enter/wait/play/exit/round` durations, event schedules, and drop/ticket/HP/reward parameters for every event across Season 6 and Season 9, comparing them against HNMO's `events.csv` and event/invasion initializer classes.

## Hand-Verified Sample Values

1. **Devil Square**:
   - Hand check in the Season 6 suite's Devil Square INI & `.dat`: `DevilSquareMaxPlayerPerRoom = 15`, `EyesOfDevilSquareDropRate = 80`, `KeyOfDevilSquareDropRate = 80`; `.dat` section `0` = `35 5 20` (`close=2100s, enter=300s, play=1200s`).
   - Hand check in the Season 9 suite's Devil Square XML: `MaxPlayerPerRoom="15"`, `DevilsEyeDropRate="80"`, `DevilsKeyDropRate="80"`, `<Time Close="35" ToOpen="5" PlayDuration="20" />`.
   - HNMO `events.csv` & `DevilSquareInitializer.cs:17`: `EnterDurationSec=300`, `GameDurationSec=1200`, `ExitDurationSec=180`, `MaxPlayers=15` -> **same core timings/limits**.
2. **Doppelganger**:
   - Hand check in both suites' `DoppelGanger.lua` & `DoppelGanger_Config.ini`: `LuaSetReadyTime(1)` (`60s`), `LuaSetPlayTime(10)` (`600s`), `LuaSetEndTime(1)` (`60s`), `DPMasterLevelExpRate = 1.0`.
   - HNMO `events.csv` & `DoppelgangerInitializer.cs:51-54`: `EnterDurationSec=30` (**differs** `30s` vs `60s`), `GameDurationSec=600`, `ExitDurationSec=60`, `MaxPlayers=5` (**same** play/exit).
3. **Imperial Guardian**:
   - Hand check in both suites' Imperial Guardian INI: `WaitPlayerTime / StandByTime = 180s`, `TimeAttackTime / RoundDurationTime = 600s` per stage, `MixNeedZen = 1000000`.
   - HNMO `events.csv` & `ImperialGuardianInitializer.cs:63-104`: `EnterDurationSec=60`, `GameDurationSec=3000` (`50m` whole-event timer), `ExitDurationSec=60`, `MaxPlayers=5` (**differs** per-stage standby/round model vs HNMO total timeout).
4. **S9-only events** (`AcheronGuardian`, `ArcaBattle`, `ChaosCastleSurvival`, `InvasionManager`, `TormentedSquare`) and suite timed/bonus events (`BonusEvent`, `GamblingLottery`, `LastManStanding`, `LorenDeep`, `RingAttack`, `XMasAttack`) are marked `missing in HNMO`.
