# Audit Findings — `server/src/GameLogic/PlugIns/TeleportNpc`

- **Unit**: `server/src/GameLogic/PlugIns/TeleportNpc` (6 files, 653 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `TeleportDecision.cs`, `TeleportDenialReason.cs`, `TeleportDestinationConfig.cs`
  - `TeleportNpcConfiguration.cs`, `TeleportNpcEngine.cs`, `TeleportNpcPlugIn.cs`

## Rules & Behavior
- Custom Teleport NPC feature (`IDisabledByDefault`, default NPCs `[247, 540]`): validates proximity (`MaximumNpcDistance = 5`), `InMiniGame`, `AllowPk`, `VipOnly` (`max(Stats.IsVip, AccountAttribute)`), `MinLevel`/`MaxLevel`, `MinResets`, `ZenCost`, `HnmoWarpGuard.Refusal`, and `WarpGateRules.RefuseAsync` before deducting Zen and warping.
- Default destinations: Lorencia (`map 0`, Lv 1, 0 Zen), Noria (`map 3`, Lv 10, 2,000 Zen), Devias out (`map 2`, Lv 20, 5,000 Zen), Atlans (`map 7`, Lv 60, 15,000 Zen), Icarus (`map 10`, Lv 170, 50,000 Zen).

## Observations
- `EvaluateWarp` checks raw `player.Level` against `dest.MinLevel` (does not apply `GetEffectiveMoveLevelRequirement` class reduction, unlike standard warps in `WarpList`). Clean guard ordering; no defects found.
