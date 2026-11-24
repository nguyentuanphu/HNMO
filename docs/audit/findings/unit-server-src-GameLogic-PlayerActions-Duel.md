# Audit: `server/src/GameLogic/PlayerActions/Duel`

- **Unit**: `server/src/GameLogic/PlayerActions/Duel`
- **Files read in full**: 3 of 3 (461 lines total)
  - `server/src/GameLogic/PlayerActions/Duel/DuelActions.cs` (349 lines)
  - `server/src/GameLogic/PlayerActions/Duel/EndDuelWhenLeavingDuelMapPlugIn.cs` (65 lines)
  - `server/src/GameLogic/PlayerActions/Duel/UpdateDuelScorePlugIn.cs` (47 lines)
- **Suite references**:
  - Season 6 suite: `the suite's common settings file` (`[DuelSystem]`), `the suite's duel arena terrain file`
  - Season 9 suite: `the suite's common settings file` (`[DuelSystem]`), `the suite's duel arena terrain file`

## Rules & Comparison

| Rule / Behaviour | HNMO | Season 6 / Season 9 Suite | Match |
|---|---|---|---|
| Duel system enable & damage reduction | Config-driven via `DuelConfiguration`; damage reduction lives in combat/damage logic rather than `DuelActions.cs` | Both S6 & S9 `the suite's common settings file` `[DuelSystem]`: `Enable = 1`, `DamageReduction = 60` | Config-referenced (damage formula audited in combat unit) |
| Duplicate duel response guard | `DuelActions.HandleDuelResponseAsync` requires `duelRoom.State == DuelState.DuelRequested` and `duelRoom.Opponent == player` before deducting `EntranceFee` or warping | Single-use pending duel state | Same (PR #305 fix) |
| Same-map, state, PK, Guild War, self-defense, NPC, level & fee checks | Checks `CurrentMap` equality, `CurrentMiniGame is not null` (requester), `HeroState >= PlayerKiller2ndStage`, `GuildWarState` (`Requested`/`Started`), `IsAnySelfDefenseActive()`, `OpenedNpc is not null`, `PlayerState.EnteredWorld`, `Level >= MinimumCharacterLevel`, `Money >= EntranceFee` | Same standard duel preconditions; PK2+ blocked | Same (`target.CurrentMiniGame` not explicitly checked, though `CurrentMap` must match) |
| Arena exclusion & `/requests off` | Blocks duel if `ArenaPlugIn.IsInBattle(player \| target)` (PR #307); blocks request if `QolPlugIn.AreRequestsBlocked(target)` | Custom HNMO features | HNMO-specific QOL/Arena rules |
| Spectators & leaving duel map | `HandleDuelChannelJoinRequestAsync` enforces `MaximumSpectatorsPerDuelRoom`; `EndDuelWhenLeavingDuelMapPlugIn` cancels duel if an alive duelist leaves the duel map during `DuelStarted`/`DuelAccepted`, and removes spectator otherwise | Standard Duel Arena map 64 spectator gates & map-leave cancel | Same |
| Score tracking | `UpdateDuelScorePlugIn` locks `duelRoom.Lock`, verifies `DuelState.DuelStarted`, increments `ScoreRequester` or `ScoreOpponent` | 1 point per kill in duel | Same |
