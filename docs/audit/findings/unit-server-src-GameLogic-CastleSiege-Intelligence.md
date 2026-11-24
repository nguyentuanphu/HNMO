# Audit Findings: `server/src/GameLogic/CastleSiege/Intelligence`

**Status**: Checked (7 files read in full, 349 lines)
**References**: [`castle-siege-s6-s9.md`](../references/castle-siege-s6-s9.md)

## Scope & Summary
Audited all 7 NPC intelligence classes: `CastleSiegeCrownIntelligence`, `CastleSiegeSwitchIntelligence`, `CastleSiegeStatueIntelligence`, `CastleSiegeGateIntelligence`, `CastleSiegeLeverIntelligence`, `CastleSiegeMachineIntelligence`, and `CastleSiegeNpcIntelligenceBase`.

## Rule & Parity Findings

1. **Crown Ownership Tracking (`CastleSiegeCrownIntelligence.cs:58-78`)**:
   - Scans attackable players within range `1` of the Crown every `1s` who belong to an attacking side (`!= None && != Defense`), preserving the current `CrownUser` as long as they stay alive and in range (no interruption by lower-ID arrivals).
   - **Parity note**: Allows *any* alive attacking-side member within range 1 to occupy `CrownUser`, rather than restricting Crown registration solely to an attacking Alliance/Guild Master.

2. **Switch Tracking (`CastleSiegeSwitchIntelligence.cs:59-77`)**:
   - Scans any siege participant (`JoinSide != None`, including defenders) within range `1` of each switch every `1s`, keeping the existing `Occupant` until they die, leave the map, or move out of range.

3. **Statue Regeneration Cadence (`CastleSiegeStatueIntelligence.cs:15, 35-40`)**:
   - Invokes `CastleSiegeStatue.Regenerate()` on a `5s` `Timer` interval while spawned.
