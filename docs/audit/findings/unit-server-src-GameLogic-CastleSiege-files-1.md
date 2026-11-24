# Audit Findings: `server/src/GameLogic/CastleSiege (files 1)`

**Status**: Checked (all 31 files read in full)
**References**: [`castle-siege-s6-s9.md`](../references/castle-siege-s6-s9.md)

## Scope & Summary
Audited all 31 core files (5,522 lines) implementing Castle Siege state machine, weekly schedule calculation, guild selection, Crown/Switch capture mechanics, tax/tribute provider, mini-map broadcasts, NPC controller, participant tracking/rewards, and admin snapshots.

## Rule & Parity Findings vs S6/S9

1. **Missing `FriendlyFire` & Siege PvP Damage Reduction (`CastleSiegePlugIn.cs:147-154`)**:
   - `CheckAttack` unconditionally cancels all attacks between players on the same `CastleSiegeJoinSide`.
   - S6/S9 `IGC_CastleSiege.xml` configures `FriendlyFire="1"` with `DamageReduction OppositeTeam="40"` and `SelfTeam="20"`; neither the `FriendlyFire` toggle nor siege team damage reduction exists here.

2. **Crown Seal Accumulation & Seal Eligibility (`CastleSiegeCrownMechanics.cs:61-84, 265-274`)**:
   - `CapAccumulatedTime` intentionally preserves shared `CrownAccumulatedTime` (capped to `CrownHoldTimeSeconds - 1`) across failed attempts and across different attacking sides; S6/S9 `ResetAccumulatedTimeAfterKill` is not modeled.
   - `ChangeWinnerGuildAsync` (`CastleSiegeCrownMechanics.cs:93-158`) properly transfers ownership to the capturing side's `AllianceMaster` guild (`WinnerGuild`), resets economy (`TaxChaos/Store/Hunt = 0`, `TributeMoney = 0`, `IsHuntZoneEnabled = false`), destroys all Life Stones, swaps sides, locks the Crown, clears switches, and respawns attackers.

3. **Weekly Schedule Model (`CastleSiegeSchedule.cs:20, 91-108`)**:
   - Models a **7-day (`WeekDuration`)** wall-clock schedule in `ServerTimeZone` (handling DST transitions cleanly via `IsInvalidTime`) with immediate transition from `EndCycle` into `RegisterGuild`.
   - S6/S9 `IGC_CastleSiege.xml` uses a **14-day** default cycle (`Days="14"`), so cycle cadence differs by configuration (data/init unit will confirm HNMO defaults).

4. **Guild Selection Formula (`CastleSiegeGuildSelector.cs:17-30, 97-135`)**:
   - Score = `(marks * 5) + memberCount + (guildMasterCombinedLevel / 4)` (using `Level + MasterLevel`, tie-breaking by `RegistrationOrder`).
   - Supports up to `MaximumSupportedAttackingSides = 3` (`Attack1..Attack3`) plus `Defense` (owner guild + alliance), expanding each selected guild's alliance automatically while skipping duplicate alliance members.

5. **Cannon Tower (`288`) & Shield (`215`) Unsupported (`CastleSiegeNpcController.cs:604-666`)**:
   - `CreateNpc` only instantiates `277` Gate, `283` Statue, `216` Crown, `217`/`218` Switches, `219` Lever, and `221`/`222` Machines; any other configured NPC number throws `NotSupportedException`. S6/S9 Cannon Towers (`288`) and Crown Shield (`215`) are missing.

6. **Taxes, Tribute & Closed-Gate Teleport Guard (`CastleSiegeTaxProvider.cs`, `CastleSiegeNpcController.cs:226-283`)**:
   - Chaos Machine & Store taxes capped at `3%`; Land of Trials entry fee capped at `300,000` Zen in `10,000` steps; owner alliance is exempt (`GetPersistentAllianceMasterGuildIdAsync`).
   - Closed gates use reference-counted tile blocking and supercover Bresenham line-of-sight (`IsLineBlockedByClosedGate`) to prevent teleporting past closed gates.
