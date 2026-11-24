# Audit Findings — `server/src/GameLogic/PlugIns/PlayerRanking`

- **Unit**: `server/src/GameLogic/PlugIns/PlayerRanking` (4 files, 567 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `PlayerRankingConfiguration.cs`, `PlayerRankingEngine.cs`, `PlayerRankingPlugIn.cs`, `StatAllocationPlan.cs`

## Rules & Behavior
- In-game ranking panel (`Shift+R` / `/rank`, `CacheSeconds = 30`, `TopCount = 20` clamped `5..100`) for characters, online players (ordered by `Resets`, `MasterLevel`, `Level`, `Name`), guilds, and mini-game leaderboards.
- Bulk Add Stats (`Shift+A`): validates `PlanBulkAddStats` (non-negative, non-empty, `Command` gated by `Stats.BaseLeadership.IncreasableByPlayer`, `total <= LevelUpPoints`, blocked inside mini-games) and chunks into `ushort.MaxValue` steps via `IncreaseStatsAction`.
- Clean implementation; no defects found.
