# Audit Findings: `server/src/GameLogic/MiniGames/Kanturu`

**Status**: Checked (all 28 files read in full)
**References**: [`kanturu-s6-s9.md`](../references/kanturu-s6-s9.md)

## Scope & Summary
Audited all 28 files (3,129 lines) covering Kanturu Maya/Nightmare battle orchestration, barrier terrain management, kill tracking, shared wave timers, Nightmare HP-phase teleports/specials, Moonstone Pendant wear & missing-pendant lethal check, and persistent Tower of Refinement reentry window.

## Rule & Parity Findings vs S6/S9

1. **Wave 2 counted monster types & target (`KanturuEventDefinition.cs:241-252` vs S6 Stage 1)**:
   - `Phase 2 - Monsters` sets `CountedMonsters = [BladeHunter (354), Dreadfear (360)]` with `KillTarget = 40`, identical to Phase 1.
   - In legacy S6 `KanturuMonsterSetBase.txt`, Stage 1 consists of `12` Twin Tail (`359`) + `8` Dreadfear (`360`) = `20` monsters, while `Phase 3 - Monsters` in HNMO sets `[Dreadfear, TwinTail]` with `KillTarget = 20`. Unless HNMO's map spawn data for Wave 3 overrides S6 Stage 1 with 40 Blade Hunters/Dreadfears, Wave 2 kill target / monster types differ from S6 Stage 1.

2. **Standby durations between Maya phases (`KanturuEventDefinition.cs:238, 265, 294`)**:
   - HNMO uses `120s` standby after Phase 1, `120s` standby after Phase 2 (`Stanby2` is `60s` in S6/S9), and `10s` after Phase 3 (matches S6/S9 `End-cycle = 10s`; note S6/S9 `Stanby3` is `120s` *before* Phase 3 monsters, whereas HNMO puts the `120s` standby *after* Phase 2).
   - `AllowEnterWhilePlaying` (`KanturuContext.cs:181-183`) permits late refills during `MayaBattle` standby windows (`_inStandby != 0`).

3. **Maya wide-area attack cadence & missing Moonstone handling (`KanturuMayaWideAttacker.cs:54-147`)**:
   - HNMO runs a deterministic `15s` timer alternating Storm (`AttackByAsync`) and Stone Rain (`ShowMayaWideAreaAttackAsync`) animations rather than probabilistic `IceStormCastRate = 60/10000` + AI phase table `[3s, 60s, 60s, 900s]`.
   - Players missing the required Moonstone Pendant are collected during `ForEachPlayerAsync` and killed afterward via `KillInstantlyAsync()` (correct lock discipline, matches S6/S9 Moonstone enforcement).

4. **Nightmare HP phases & special attack (`KanturuNightmareRunner.cs:160-269`, `KanturuEventDefinition.cs:347-373`)**:
   - Monotonic HP threshold transitions (`<75%` -> `(79,100)` + Wave 9, `<50%` -> `(78,124)` + Wave 10, `<25%` -> `(78,141)` + Wave 11) via `KanturuNightmarePhaseSelector` replace S6/S9 AI Group `23`, plus `20s` visual Inferno (`Skill 14`) animation. Wait delay of `500ms` before teleport lets health update arrive cleanly.

5. **Tower of Refinement closing linger vs S6/S9 (`KanturuContext.cs:736-756`)**:
   - HNMO runs `Revitalization` for `23h` (warning `5m` before end), then broadcasts `Notify` -> `Close`, clears persisted `TowerOpenUntilUtc`, and ends immediately rather than lingering for S6/S9's extra `2220s + 180s + 5s` post-close cycle.
   - Positive design: `OpenElphisBarrierAsync` (`KanturuContext.cs:603-642`) uses `Interlocked`, persists `TowerOpenUntilUtc` first, removes `NoGround` on `(73..90, 144..195)` server-side (`KanturuBarrierAreaHelper` avoids `byte` overflow), sends fallback terrain packet, and `_stateLock` prevents late battle broadcasts from overwriting `Tower`.
