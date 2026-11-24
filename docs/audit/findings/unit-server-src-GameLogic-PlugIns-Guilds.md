# Audit Findings: `server/src/GameLogic/PlugIns/Guilds`

- **Unit**: `server/src/GameLogic/PlugIns/Guilds` (18 files, 2,202 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference**: HNMO custom feature (`GuildsPlugIn`, no direct S6/S9 suite equivalent). Also cross-checked against [guild-and-alliance-s6-s9.md](../references/guild-and-alliance-s6-s9.md).

## Files Reviewed (in full)
1. `GuildEngine.cs` (1–548)
2. `GuildKey.cs` (1–16)
3. `GuildLadderEntry.cs` (1–16)
4. `GuildLadderLine.cs` (1–38)
5. `GuildLadderReward.cs` (1–41)
6. `GuildLevelDefinition.cs` (1–39)
7. `GuildOwedReward.cs` (1–32)
8. `GuildProgress.cs` (1–38)
9. `GuildSiegeResult.cs` (1–26)
10. `GuildsConfiguration.cs` (1–170)
11. `GuildsPlugIn.cs` (1–807)
12. `GuildStateEvent.cs` (1–21)
13. `GuildStateWriter.cs` (1–147)
14. `GuildStore.cs` (1–133)
15. `GuildStoreData.cs` (1–44)
16. `GuildWeekResult.cs` (1–26)
17. `GuildXpResult.cs` (1–20)
18. `GuildXpSourceSetting.cs` (1–22)

## Rules & Behavior Summary
- `GuildEngine` / `GuildsConfiguration`: Pure state machine tracking cumulative guild XP, daily XP cap per member (`400`, exempting `CastleSiege`), XP sources (`MonsterKill=1`, `EventFinish=30`, `EventWin=60`, `WorldBossKill=50`, `WorldBossDamage=2/%`, `Mission=15`, `OnlineTime=5` per `10 min`, `CastleSiege=3000` + `5000` weekly ladder bonus), 10 guild levels (`0..260,000` XP granting up to `+5%` bonus EXP, `+10%` Zen multiplier, `+40` `DefenseFinal`), Monday 00:00 `Australia/Sydney` weekly ladder rollover keeping Top 10, and Top 5 GoblinPoint rewards (`150+100`, `100+50`, `60+30`, `30`, `30` with `LadderMinimumXp = 100`).
- `GuildsPlugIn`: Refreshes hidden buff (`BuffEffectNumber = 240`, `5 min` duration) on map tick (`1 min`) and level-up; distributes weekly coin/item/zen rewards once via idempotent `PaidReferences` and `AddOwed`/`TakeOwed` (all-or-nothing bag delivery); ingests Arena guild XP from `ArenaShared.Current` on the primary server (`!HnmoSeasonInstance.IsSecondary`).
- `GuildStore`: Persists guild progress, daily totals, and owed rewards into `HnmoPlayerStateRowBackend` rows plus server metadata in `HnmoStateStore` under `Lock`.

## Observations
1. **Master tick ownership across servers (`GuildsPlugIn.cs:267-271`)**: Weekly payouts and `TakeArenaGuildXp` run only on `serverId == Contexts.Keys.Min()`. Assumes the process keeps a single `GuildStore` / `_engine` per file path and that the lowest server ID stays active long enough to process Monday rollovers.
2. **All-or-nothing item reward delivery (`GuildsPlugIn.cs:414-474`)**: Properly rolls back added inventory items if inventory space or `TryAddMoney` (Zen cap) fails, storing unclaimed rewards in `Owed` until `ClaimAsync` or next login/tick.
3. **Custom HNMO progression module**: Cleanly separated from core S6/S9 guild rules (`PlayerActions/Guild`); no discrepancies against legacy specs.
