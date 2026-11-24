# Audit Findings: `server/src/GameLogic/PlayerActions/Guild`

- **Unit**: `server/src/GameLogic/PlayerActions/Guild` (11 files, 1,160 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference saved**: [guild-and-alliance-s6-s9.md](../references/guild-and-alliance-s6-s9.md)

## Files Reviewed (in full)
1. `GuildCreateAction.cs` (1–97)
2. `GuildInfoRequestAction.cs` (1–23)
3. `GuildKickPlayerAction.cs` (1–107)
4. `GuildListRequestAction.cs` (1–38)
5. `GuildMasterAnswerAction.cs` (1–50)
6. `GuildRelationshipChangeAction.cs` (1–244)
7. `GuildRequestAction.cs` (1–76)
8. `GuildRequestAnswerAction.cs` (1–72)
9. `GuildRoleAssignAction.cs` (1–145)
10. `GuildWarAnswerAction.cs` (1–214)
11. `GuildWarRequestAction.cs` (1–91)

## Rules & Behavior Summary
- `GuildCreateAction`: Enforces `PlayerState.EnteredWorld`, no existing guild, `GuildLimitsPlugIn.GetMinimumCreateLevel`, non-empty name, `BannedWordsPlugIn`, `IGuildCreatingPlugIn`, and uniqueness on `IGuildServer`.
- `GuildKickPlayerAction`: Checks security code (or password hash if `SecurityCode` is unset), blocks non-masters from kicking others, blocks Guild Master disband while owning the castle (`OwnsTheCastleAsync`), and calls `KickMemberAsync`.
- `GuildRequestAction` / `GuildRequestAnswerAction`: Enforces Lv6+ requester, checks `IGuildJoinRequestingPlugIn` twice (request and accept), sets `NormalMember`, notifies requester.
- `GuildRoleAssignAction`: Enforces max `1` Assistant Master and `GuildRoleLimits.MaxBattleMasterCount(masterTotalLevel)` server-side regardless of client byte.
- `GuildRelationshipChangeAction`: Handles Alliance Join/Leave and Hostility Join/Leave, enforcing `Stats.MaximumAllianceSize` and `IGuildRelationshipChangingPlugIn`.
- `GuildWarRequestAction` / `GuildWarAnswerAction`: Starts Normal War (`MaximumScore = 20`) or Battle Soccer (`MaximumScore = 100`), warps parties to Soccer arena, adds `+1` guild score to the winner.

## Discrepancies & Gaps Found
1. **Missing `AllianceMinGuildMember` check (`GuildRelationshipChangeAction.cs:70-103`)**: S6/S9 `the suite's common settings file` requires `AllianceMinGuildMember = 10` members per guild to form or join an alliance; HNMO checks `MaximumAllianceSize` but never checks minimum member counts of either guild.
2. **Guild member cap not enforced in `GuildRequestAction` / `GuildRequestAnswerAction`**: `GuildRequestAnswerAction.cs:66` calls `guildServer.CreateGuildMemberAsync(...)` (which returns `Task`, not a success boolean) and immediately sends `GuildRequestAnswerResult.Accepted` without checking `Stats.MaximumGuildSize` (`MaxGuildMember = 80`) or guild full status.
3. **Hostility/Alliance vs Castle Siege or Alliance Hostility edge cases (`GuildRelationshipChangeAction.cs:140-152`)**: `RequestLeaveAllianceAsync` with a non-empty `targetGuildName` passes `GetGuildIdByNameAsync(targetGuildName)` directly to `RemoveAllianceAsync(targetGuildId)` without validating here that `targetGuildName` belongs to the caller's alliance (relies on `GuildServer`).
4. **Castle-during-siege disband guard (`GuildKickPlayerAction.cs:74,97-106`)**: Blocks disband when `SiegeData.IsOccupied && OwnerGuildId == persistentGuildId`; S6/S9 `CastleOwnerGuildDestroyLimit = 1` also blocks disbanding during active Castle Siege for participants.
5. **Guild Creation error code reuse (`GuildCreateAction.cs:46,54,60`)**: Returns `GuildCreateErrorDetail.GuildAlreadyExist` for low level, empty name, or banned word because protocol enum lacks finer error codes (documented in comment on line 52).
