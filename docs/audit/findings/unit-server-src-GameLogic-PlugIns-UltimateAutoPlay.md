# Audit Findings — `server/src/GameLogic/PlugIns/UltimateAutoPlay`

- **Unit**: `server/src/GameLogic/PlugIns/UltimateAutoPlay` (3 files, 294 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `UltimateAutoPlayConfiguration.cs`, `UltimateAutoPlayPlugIn.cs`, `UltimateAutoPlayPolicyWriter.cs`

## Rules & Behavior
- Resolves per-feature `Off / Free / VipOnly` policy (`SparkleGuide` and `PresetsAndStats` default `Free`; `ManaSdPotions`, `JewelDeposit`, `TargetPriority`, `Patrol`, `Survival`, `BossFocus` default `VipOnly`) against `Stats.IsVip` (character attribute or account attribute).
- Polls every `5s` for VIP state changes and broadcasts updated JSON (`F5 57`) immediately when admin config or plugin activation changes. No defects found.
