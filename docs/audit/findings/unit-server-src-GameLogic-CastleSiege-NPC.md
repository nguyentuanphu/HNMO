# Audit Findings: `server/src/GameLogic/CastleSiege/NPC`

**Status**: Checked (all 11 files read in full)
**References**: [`castle-siege-s6-s9.md`](../references/castle-siege-s6-s9.md)

## Scope & Summary
Audited all 11 NPC runtime classes (1,078 lines): `CastleSiegeAttackableNpc`, `CastleSiegeGate`, `CastleSiegeStatue`, `CastleSiegeCrown`, `CastleSiegeSwitch`, `CastleSiegeLever`, `CastleSiegeMachine`, `CastleSiegeLifeStone`, `CastleSiegeNpcBase`, `CastleSiegeCrownState`, and `ICastleSiegeNpc`.

## Rule & Parity Findings

1. **Gate Terrain Collision (`CastleSiegeGate.cs:143-152`)**:
   - Each closed gate blocks a horizontal `6x2` tile footprint anchored at `(Position.X - 3 .. Position.X + 2, Position.Y .. Position.Y + 1)` and broadcasts `IChangeTerrainAttributesViewPlugin` + gate open/close state.

2. **Life Stone Creation & Healing (`CastleSiegeLifeStone.cs:24-31, 173-201`)**:
   - Takes `5` build stages at `12s` intervals (`60s` total) and packs build stage + friendly bit (`0x8000`) into `GetTypeNumberFlags`.
   - Once active (`BuildTime >= 5`), heals `1%` of `MaximumHealth`, `MaximumMana`, and `MaximumAbility` every `1s` for alive same-side players within range `3`.
   - Only attackable during `CastleSiegeState.Start` by an opposing `JoinSide` (`CanBeAttackedBy`), and `OnDeathAsync` skips drops/XP/respawn.

3. **Attackable Structure Protections (`CastleSiegeAttackableNpc.cs:118-133, 163-170`)**:
   - Gates and Statues ignore reflect, poison, and bleeding damage, and only accept attacks from non-`None`/non-`Defense` siege players during `CastleSiegeState.Start` (supports player surrogates/summons via `IPlayerSurrogate.Owner`).

4. **Guardian Statue Regeneration (`CastleSiegeStatue.cs:76-94`)**:
   - When alive and `RegenLevel > 0`, restores `Math.Max(1, MaximumHealth * RegenPercent / 100)` per `5s` tick.
