# Audit Findings: `server/src/GameLogic/Crywolf`

**Status**: Checked (all 23 files read in full)
**References**: [`crywolf-s6-s9.md`](../references/crywolf-s6-s9.md)

## Scope & Summary
Audited all 23 files (3,913 lines) covering the Crywolf Fortress defense state machine, Elf altar contract mechanics, Balgass & Dark Elf waypoint movement/skills/revive AI, ballista bombardment, MVP scoring/ranking, peace benefits (`ChaosRateBenefit`, `MonsterHealthBenefitPercentage`), and occupation penalties (`JewelDropPenaltyPercentage`, `ExperiencePenaltyPercentage`).

## Rule & Parity Findings vs S6/S9

1. **Exact Core Numerics Match S6/S9 `the suite's Crywolf config file` (`CrywolfEventDefinition.cs`)**:
   - State durations: `Notify1 = 10m`, `Notify2 = 5m`, `Ready = 150s`, `Start = 15m`, `End = 15s`, `EndCycle = 10s`.
   - Battle triggers: `StatueAttackDelay = 5m`, `BalgassAppearanceDelay = 10m`, Balgass wave `5`, army waves `1..4, 6..13` + 6 ballistas + `CrywolfMovePath` waypoints.
   - Scores (`7000 / 6000 / 3000 / 1000 / 700 / 600 / 600`), rank thresholds (`0 / 1001 / 3001 / 5001 / 10001`), and rank EXP (`0 / 200k / 800k / 1.2M / 1.8M`) match S6/S9 verbatim.
   - Benefits (`IsBenefitActive = true`, `ChaosRateBenefit = 5%`, `MonsterHealthBenefitPercentage = 90%`) and Penalties (`IsPenaltyActive = false`, `JewelDropPenaltyPercentage = 70%`, `ExperiencePenaltyPercentage = 100%`) match S6/S9 defaults. (`KundunHPRefill` is omitted, matching default `0`).

2. **Documented Intentional Behavioral Differences**:
   - `CrywolfContext.cs:410-413`: Altars can only be contracted during `Ready` or `Start` states (original allows attempting earlier, which was prevented).
   - `CrywolfContext.cs:809-822`: Hero list packet is sent during `End` rather than `EndCycle` so high-framerate clients always display heroes before dialog rendering.
   - `CrywolfEventDefinition.cs:36-41`: Default schedule runs `Wednesday` and `Saturday` at `20:30` server time rather than the legacy daily `02:10` / `04:10` sample times.

3. **Altar & Multi-Server Lifecycle (`CrywolfAltar.cs`, `CrywolfContext.cs`)**:
   - Altar eligibility checks Elf classes `8, 10, 11`, minimum level `260`, no mount (`Uniria`/`Dinorant`/`Fenrir`), `10s` cooldown, exact altar tile coordinate, `5s` hold delay, `2` max attempts/contracts (`ContractCount` increments on attempt).
   - Cross-server occupation synchronization reads `CrywolfData` every `5s` (`SynchronizeWithEventServerAsync`) with `IsWarOverdue` fallback if the primary Crywolf server shuts down during battle.
   - Monster HP multiplier (`CrywolfPlugIn.cs:224-231`) properly excludes player summons and fixed-HP event gates (`MaximumHealthOverride != null`).
