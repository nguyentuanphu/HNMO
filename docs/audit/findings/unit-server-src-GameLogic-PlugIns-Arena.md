# Unit Audit: `server/src/GameLogic/PlugIns/Arena`

- **Checked at**: `6a47bdb6` (2026-11-24)
- **Status**: `Checked`
- **Files read in full (25)**:
  - `server/src/GameLogic/PlugIns/Arena/ArenaClassTemplate.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaConfiguration.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaDuels.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaGuildRecord.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaGuildWar.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaGuildWarEngine.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaHeraldPlugIn.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaPlayerRecord.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaPlugIn.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaRanking.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaRoyale.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaRoyaleEngine.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaRules.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaScheduleNoticesPlugIn.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaShopConfiguration.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaShopItem.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaShopPlugIn.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaStore.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaStoreData.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaTeamEngine.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaTeamRulesPlugIn.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaTeams.cs`
  - `server/src/GameLogic/PlugIns/Arena/ArenaTemplateItem.cs`
  - `server/src/GameLogic/PlugIns/Arena/StartGuildWarChatCommandPlugIn.cs`
  - `server/src/GameLogic/PlugIns/Arena/StartRoyaleChatCommandPlugIn.cs`

## Scope & Reference
HNMO Arena (`HnmoSeasonInstance.IsArena`) is an HNMO-specific cross-season PvP mode rather than a Season 6 / Season 9 legacy feature, so there are no legacy suite tables to compare against directly. Audited for internal consistency, safety guards, PK/attack/spawn rules, and alignment with project rules.

## Observations
1. **Ranked 1v1 Duels (`ArenaDuels.cs`, `ArenaRanking.cs`)**:
   - Uses `DuelActions.HandleDuelResponseAsync` with `RoundsToWin = 2` (best of 3), Elo `StartRating = 1000`, `K = 32`, Sydney ISO week/day, and `MatchGap = min(1000, 100 + 50 * floor(waitSeconds / 10))`.
   - Awards `PointsPerWin = 5`, `PointsPerLoss = 2`, capped at `DailyPointsCap = 100`.
2. **5v5 Team Battles (`ArenaTeams.cs`, `ArenaTeamEngine.cs`, `ArenaTeamRulesPlugIn.cs`)**:
   - Supports solo or party queues (up to `TeamSize = 5`, falling back to `TeamMinSize = 3` after `SmallerBattleWait = 60s`).
   - Minor note (non-bug): `ArenaTeamEngine.FormTeams` comment mentions balancing by lower total rating on a tie and `Group` carries `Rating`, while `ArenaTeams.TickAsync` passes `0` for `Rating` and `FormTeams` breaks ties by `aCount <= bCount`.
   - Friendly fire is blocked, opposing kills are exempt from PK penalties, fallen players respawn at their team's gate after `TeamRespawnSeconds = 5`, and `TeamPointsPerWin = 8` / `TeamPointsPerLoss = 3` (draws also award 3).
3. **Battle Royale (`ArenaRoyale.cs`, `ArenaRoyaleEngine.cs`)**:
   - Runs on `RoyaleMapNumber = 0` at `12:00, 20:00, 22:00` Sydney time (or `/startroyale`), 5-minute sign-up, 3–50 players, 12-minute duration, 2-minute grace, 8-minute shrink from radius 120 to 6, 2%–10% HP damage outside the zone, 20 pts to winner / 2 pts to others.
4. **Guild War (`ArenaGuildWar.cs`, `ArenaGuildWarEngine.cs`)**:
   - Runs Saturday/Sunday 21:00 Sydney time (or `/startguildwar`) on `TeamMapNumber = 30`, 5–10 members per guild, 3 capture flags (`79,27; 89,27; 100,33`), 15 pts win / 4 pts loss, 500/150 guild XP, 1000 weekly champion XP.
5. **Arena Shop & Rules (`ArenaShopPlugIn.cs`, `ArenaRules.cs`)**:
   - Shop is disabled inside Arena (`!ArenaRules.Active`) and refunds points if inventory add or persistence save fails.
   - `ArenaRules` blocks Trade, Store, Drop, PickUp, Warp, Command, CashShop, Vault in Arena while allowing F5 subcodes `0x52` (ArenaTravel) and `0x54` (BugReport).
   - `ArenaTeamRulesPlugIn.SelectSpawnGateAsync` falls back to `cfg.HomeGate` on Map 6 so players do not respawn on blocked tiles.

## Verdict
No game-rule or persistence defects found.
