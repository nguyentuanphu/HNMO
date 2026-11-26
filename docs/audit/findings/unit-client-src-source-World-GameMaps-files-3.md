# Unit Audit: `client/src/source/World/GameMaps (files 3)`

- **Unit**: `client/src/source/World/GameMaps (files 3)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `9` (`5,883` lines): `GMEmpireGuardian2..4.*`, `GMGmArea.h`, `GMHellas.*` (`2,031L`)
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Kalima 1..7 entry tier selection & class discount | `GMHellas.cpp:145-173` | Matches Hero level against `g_iKalimaLevel[startIndex + i]` (`startIndex = NUM_HELLAS` for MG/DL/RF lower brackets `[20, 111, 161, 211, 261, 311, 350]` vs standard `[40, 131, 181, 231, 281, 331, 350]`) | Web `kalima.ts:36-40` | Same |
| Lost Map SafeZone block & Symbol of Kundun `5`-count check | `GMHellas.cpp:175-265` | Blocks Lost Map use inside `Hero->SafeZone` (`CanTBeUsedInTheSafeZone`); requires min Kalima 1 level; Symbol of Kundun tooltip requires `Durability >= 5` to form Lost Map | Web `kalima.ts:56-85` | Same |
| Imperial Guardian visual map objects | `GMEmpireGuardian2.cpp`, `GMEmpireGuardian3.cpp`, `GMEmpireGuardian4.cpp`, `GMGmArea.h` | Weather/portal/joint effects across Imperial Guardian instances (actual wave/day rules are server-side in `MiniGames/ImperialGuardian`) | Visual presentation | No game rules |
