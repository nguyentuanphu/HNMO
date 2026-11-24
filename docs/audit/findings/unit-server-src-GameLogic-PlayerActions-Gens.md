# Audit Findings: `server/src/GameLogic/PlayerActions/Gens`

- **Unit**: `server/src/GameLogic/PlayerActions/Gens` (1 file, 333 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference saved**: [gens-system-s6-s9.md](../references/gens-system-s6-s9.md)

## Files Reviewed (in full)
1. `GensActions.cs` (1–333)

## Rules & Behavior Summary
- `JoinAsync`: Verifies active `GensFeaturePlugIn`, opened NPC matches requested `GensType` (`543` Duprian / `544` Vanert), runs inside `RunPersistenceExclusiveAsync` to enforce not already joined, `RejoinWaitTime` elapsed, `Level >= MinimumLevel` (`50`), not in party (`Party is null`), and not in a guild (`GuildStatus is null`), then sets `StartingContribution` (`10`), `StartingRank` (`14`), `RankingPosition = 0`, saves, and updates views/observers.
- `LeaveAsync`: Verifies opened Gens NPC matches `member.Gens`, blocks guild masters (`GuildMaster`), clears Gens fields and sets `LeftAt`, saves, and notifies views/observers.
- `ShowInfoAsync`: Sends `ShowGensInfoAsync()` when `GensFeaturePlugIn` is active.
- `RequestRewardAsync`: Runs exclusively per player; checks joined Gens matching NPC, UTC day within `[RewardStartDay, RewardEndDay]` (`1..7`), not already claimed in current UTC year/month, reward configured for current `Rank` (`1..8`), and atomic inventory space allocation via `TryAddRewardItemsAsync` (rolls back all added items if any fails), marking `RewardClaimedAt` before emitting `ItemAppearAsync`.

## Discrepancies & Observations
1. **Non-master guild member leaving Gens (`GensActions.cs:314-332`)**: `CheckLeaveRequirements` blocks `GuildPosition.GuildMaster`, but if `GensConfiguration.GuildRequiresGens = true`, normal guild members can still leave Gens (leaving them in a guild while `Gens == None` or switching sides when `RejoinWaitTime == 0`).
2. **Default `RejoinWaitTime = TimeSpan.Zero` (`GensConfiguration.cs:33`)**: Allows instant rejoining or switching Gens immediately after leaving unless configured otherwise.
