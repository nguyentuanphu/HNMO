# Audit Findings — `server/src/GuildServer`

- **Unit**: `server/src/GuildServer` (4 source files + `.csproj`, 1,412 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `GuildChangeToGameServerPublisher.cs`, `GuildContainer.cs`, `GuildServer.cs`, `IGuildChangePublisher.cs`

## Rules & Behavior
- **Role & Alliance Constraints (`GuildServer.cs:647-707, 1106-1124`)**:
  - Only `NormalMember`, `BattleMaster`, and `AssistantMaster` can be assigned via `ChangeGuildMemberPosition*`; `GuildMaster` transfer and promotion to `GuildMaster` are rejected.
  - At most `1` `AssistantMaster` per guild; `BattleMaster` count is gated by `GuildRoleLimits.MaxBattleMasterCount(masterTotalLevel)`.
  - Guild Master being kicked / leaving triggers full `DeleteGuildAsync` (`lines 361-365`), disbanding any alliance first (`lines 755-764`).
  - Removing the second-to-last alliance member (`remainingAllianceGuilds.Count < 2`) clears the master guild's alliance self-reference (`lines 480-487`).
- **Transitive Hostility (`lines 929-1022`)**:
  - Same-alliance `Union` overrides prior direct hostility (`lines 947-951`). Direct hostility propagates transitively across all member guilds of both alliances and reconciles on load/unload/disband (`PublishRivalryChangesAsync`).

## Findings & Observations
1. **Incomplete War Teardown — `GuildServer.cs:773`**:
   - `DeleteGuildAsync` has an open `// TODO: Inform game servers that a guild war ended.` when a guild disbands mid-war.
2. **Missing Lock on Member Mutations — `GuildServer.cs:179-207, 210-290, 345-377`**:
   - `CreateGuildMemberAsync`, `ChangeGuildMemberPosition*`, and `KickMemberAsync` mutate `guild.Guild`, `guild.Members`, and `guild.DatabaseContext` without acquiring `_relationshipChangeLock`, unlike `CreateGuildAsync`, `DeleteGuildAsync`, and `GuildMemberLeftGameAsync`, risking concurrent `DatabaseContext` / dictionary mutations if a guild member joins/leaves/gets kicked concurrently.
3. **Guild Member Cap**: `GuildServer.CreateGuildMemberAsync` does not enforce a max member cap (S6/S9 suite `MaxGuildMember = 80`); relies on game-server caller validation (`GuildRequestAnswerAction` — verify there).
