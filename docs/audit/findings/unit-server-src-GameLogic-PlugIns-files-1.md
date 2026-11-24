# Audit Findings: `server/src/GameLogic/PlugIns (files 1)`

- **Unit**: `server/src/GameLogic/PlugIns (files 1)` (123 files)
- **Checked on**: 2026-11-24
- **Status**: `Checked`

## Summary

All 123 files in `server/src/GameLogic/PlugIns (files 1)` were read in full. This unit contains core gameplay plugin implementations (death penalties, pet experience, monster scaling/amount, attack speed/ping validation, mini-game/NPC talk helpers, state retention/warp guard, bank top-up, learn skills by level, periodic save, login/connection limits, guild war/chat bridge) and the 64 `I*PlugIn` plugin-point interface contracts.

## Rules & Cross-Checks

- **Death Experience Loss (`PlayerLosesExperienceAfterDeathPlugIn`)**:
  - Applies only when killed by a `Monster`, outside mini-games, without `Stats.DeathPenaltyProtection`, and when Crywolf is not active/exempting experience loss.
  - Default rates align with Season 6/9 server suites: 3% (levels 11-150), 2% (151-220), 1% (221+ and Master), 5% (PK Warning), 10% (PK Stage 1), 20% (PK Stage 2). Clamped to current level floor (no de-leveling).
- **Death Zen Loss (`PlayerLosesMoneyAfterDeathPlugIn`)**:
  - Applies only when killed by a `Monster`, outside mini-games, without `Stats.DeathPenaltyProtection`.
  - Default rates align with Season 6/9 server suites: inventory zen loss 1% (10+), 2% (150+), 3% (220+), 4% (Master); vault loss 0%.
- **Pet Experience (`PetExperiencePlugIn`)**:
  - `PetShare = 0.2` (20% of player kill XP; split evenly if both `PetSlot` and `RightHandSlot` hold trainable pets).
  - Dark Raven level-ups enforce `GetDarkRavenLeadershipRequirement(pet.Level + 1) <= player.Attributes[Stats.TotalLeadership]`.
- **Ping & Clock Validation (`PingCheck`, `PingClockValidator`)**:
  - Observe-only (`LogDebug`, no kicks/sanctions). Uses `unchecked(current - previous)` for 32-bit tick wrap safety and treats deltas `> 3_600_000 ms` as client clock resets.
- **Quest Monster Kill Count (`QuestMonsterKillCountPlugIn`)**:
  - Increments `KillCount` for active quest monster requirements on monster kill and displays progress via `ShowBlueMessageAsync` up to `MinimumNumber`.

## Observations / Minor Notes

- `PlayerLosesExperienceAfterDeathPlugInConfiguration.cs`: XML doc comment on class references `QuestMonsterKillCountPlugInConfiguration` (copy-paste doc typo, non-functional).
- No critical gameplay defects found in the remaining concrete files or plugin interfaces of this unit.
