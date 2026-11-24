# Unit Audit: `client/src/source/GameLogic/Events`

- **Unit**: `client/src/source/GameLogic/Events`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `18` (`5,654` lines) including `CSEventMatch.cpp`, `CSChaosCastle.cpp`, `NewBloodCastleSystem.cpp`, `SiegeRegistration.cpp`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Event match countdown & Doppelganger priority guard | `CSEventMatch.cpp:27,140-193` | Tracks countdown timer `kMatchCountdownDuration`; blocks non-Doppelganger countdown types from overriding active Doppelganger countdown (`TYPE_MATCH_DOPPELGANGER_ENTER_CLOSE..CLOSE`) | Server `MiniGames` countdown packets | Same |
| Chaos Castle stealth armor & helper stripping | `CSChaosCastle.cpp:119-126` | Strips wing upgrade glow level (`0`), hides helpers (`Helper.Type = -1`), and zeroes visible weapon upgrade levels in Chaos Castle appearance | Web `main.ts:1482` & Chaos Castle rules | Same |
| Castle Siege registration minimum level | `SiegeRegistration.cpp:15` | Requires `CharacterAttribute->Level >= DeclareLevel` (`200`) | Web `castleSiege.ts:31` (`DECLARE_LEVEL = 200`) & Server `CastleSiege` | Same |
| Blood Castle match result display | `NewBloodCastleSystem.cpp:126-151` | Reports `EXP`, optional `Zen` (only when `m_iNumResult != 0` won), and `Blood Castle Point` score | Server `BloodCastleContext` & Web `eventWindows.ts` | Same |
