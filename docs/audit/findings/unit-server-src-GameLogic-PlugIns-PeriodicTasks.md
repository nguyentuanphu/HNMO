# Audit Unit: `server/src/GameLogic/PlugIns/PeriodicTasks`

- **Status**: Checked (on commit `0b67af2e`, 2026-11-24)
- **Files**: 28 files read in full (mini-game starters `BloodCastle`, `DevilSquare`, `ChaosCastle`, `IllusionTemple`, `Kanturu`, `HappyHourPlugIn`, `DurabilityDecayPlugIn`, `ShieldRecoveryHiatusPlugIn`, `PeriodicTaskBasePlugIn`, `MiniGameStartBasePlugIn`, `UpcomingEvents`, and configuration/state classes).
- **Rule Category**: Scheduled events, Happy Hour, durability decay, and SD recovery hiatus.

## Findings

1. **Scheduler & event lifecycle**: `PeriodicTaskBasePlugIn` uses a generation counter (`_forceGeneration`) for forced starts, DST-safe `GetNextStartUtc`, and per-context `AllowedServerIds` gating. `MiniGameStartBasePlugIn` drives entrance countdowns on ticks without background sleep loops and disposes stale/ended instances before starting new runs.
2. **S6/S9 parity**: `HappyHourConfiguration.Default` (`02:00, 08:00, 14:00, 20:00`, 2h, `1.5x` EXP, `+15%` drop, `+5/10000` exc) and `DurabilityDecayPlugIn` (`WearPeriodSeconds = 10`, Wizard's Ring `70`, Moonstone `63`) match Season 6/9 behavior. No defects found.
