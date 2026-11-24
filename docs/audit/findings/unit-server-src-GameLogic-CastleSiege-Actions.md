# Audit Findings: `server/src/GameLogic/CastleSiege/Actions`

**Status**: Checked (all 22 files read in full)
**References**: [`castle-siege-s6-s9.md`](../references/castle-siege-s6-s9.md)

## Scope & Summary
Audited all 22 action handlers (1,950 lines) covering guild registration/unregistration, Mark of the Lord submission, registration state/status queries, gate lever operations, NPC list/buy/repair/upgrade, Land of Trials access toggle/entry, tax info/rate change, tribute withdrawal, Life Stone summoning, warfare machine firing, and alliance-master mini-map commands.

## Rule & Parity Findings

1. **Warfare Machine Impact Uses Operator Damage Pipeline (`CastleSiegeMachineUseAction.cs:193-198`)**:
   - Catapult attacks select a random coordinate in the `targetZoneIndex` rect, broadcast hit notifications within range `6`, wait `1500ms`, then apply `attackable.AttackByAsync(player, null, false)` to non-friendly active targets outside safezone within range `3` using the operator's combat stats rather than fixed catapult damage from S6/S9 server skill data.

2. **Authorization & Concurrency Discipline (Solid)**:
   - `CastleSiegeGuildResolver` requires `GuildMaster` who is also the `AllianceMaster` (if in an alliance) for guild/mark registration (`RegisterGuild`, `UnregisterGuild`, `RegisterMark`).
   - `CastleSiegeNpcAuthorization` restricts buy/repair/upgrade/NPC list to owner `GuildMaster`/`AssistantMaster`, while gate levers allow `Defense` side during battle and any owner-alliance member outside battle (`CastleSiegeGateOperateAction:81-85` also enforces `player.OpenedNpc` matching the lever's gate).
   - `CastleSiegeGuildCommandAction` enforces a 1-second per-player atomic cooldown (`NextCommandTicks`) and bounds `team < 7` (`CommandGroupCount`) before taking `ExecutionLock`.
   - `CastleSiegeTributeWithdrawAction` performs rollback (`TributeMoney = previousTribute` + `TryRemoveMoney`) if `SaveOwnerAsync` throws, keeping Zen and treasury atomic.
   - `CastleSiegeRegisterMarkAction` increments persistent marks before destroying the `SignOfLord` item so database failure never consumes the item without credit.
