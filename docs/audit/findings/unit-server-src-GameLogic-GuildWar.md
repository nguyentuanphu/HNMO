# Audit Findings: `server/src/GameLogic/GuildWar`

**Status**: Checked (all 5 files read in full, 237 lines)
**References**: [`guild-and-alliance-s6-s9.md`](../references/guild-and-alliance-s6-s9.md), `server/src/GameLogic/PlayerActions/Guild`

## Scope Covered
- `GuildWarContext.cs` (66 lines)
- `GuildWarScore.cs` (103 lines)
- `GuildWarState.cs` (26 lines)
- `GuildWarTeam.cs` (21 lines)
- `GuildWarType.cs` (21 lines)

## Rule & Suite Comparison

| File & Line | Rule / Formula | Suite / Reference Role & Value | Verdict |
|---|---|---|---|
| `GuildWarType.cs:10-21` | Supports `Normal` (map combat guild war) and `Soccer` (`SoccerGameMap` Battle Soccer match). | S6/S9 suites support Guild War (`EnableBattleSoccer = 1`) and Battle Soccer in the stadium arena. | Same |
| `GuildWarState.cs:10-26` | State machine: `Requested` -> `Started` -> `Ended`. | Matches classic Guild War lifecycle (`Request` pending, active `Started`, finished `Ended`). | Same |
| `GuildWarContext.cs:19-66` | Holds per-player war context (`WarType`, `Score`, `Team`, `Requester`, mutable `State`), projecting `EnemyTeamName`, `ThisScore`, `EnemyScore` relative to `First` vs `Second` team. | Matches packet view projection for client score HUD. | Same |
| `GuildWarScore.cs:49-54` | `HasEnded` checks `_firstGuildScore >= MaximumScore \|\| _secondGuildScore >= MaximumScore` (`MaximumScore = 20` Normal / `100` Soccer configured in guild war start action). | Classic MU Guild War target scores: `20` points for Normal War, `100` points for Battle Soccer. | Same |
| `GuildWarScore.cs:65-97` | `IncreaseFirstGuildScore` / `IncreaseSecondGuildScore`: increments `_firstGuildScore`/`_secondGuildScore` via `Interlocked.Add`, raises `PropertyChanged`, sets `Winners` and clears `PropertyChanged = null` upon reaching `MaximumScore`. Note minor observation: `HasEnded` check and `Interlocked.Add` + `Winners` assignment are not inside one atomic lock/CAS, so near-simultaneous kills at `MaximumScore - 1` could both increment and race setting `Winners` (practically guarded by single-map thread scheduling). Also `FirstGuildScore`/`SecondGuildScore` clamp at `byte.MaxValue` (`255`). | Functional match for Battle Soccer (+2/+20 goal values) and Normal War (+1/+2 kill values); no gameplay break observed under normal single-map execution. | Same (minor race hardening note) |

## Bugs / Open Issues
- **Low-risk concurrency observation (`GuildWarScore.cs:65-97`)**: `IncreaseFirstGuildScore` and `IncreaseSecondGuildScore` check `HasEnded` outside `Interlocked.Add`; if two map threads or simultaneous events hit `MaximumScore` at the same instant, `Winners` could be overwritten. Low priority since guild score updates normally occur sequentially on one map loop.
