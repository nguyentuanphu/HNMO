# Audit Findings: `client/src/source` `Data`, `Network`, & `World` (39 Units)

Checked on: `2026-11-24`

## Units Covered
- `client/src/source/Data/*` (27 units: `DataHandler/*`, `GameConfig`, `GameData/*`, `Translation`)
- `client/src/source/Network/*` (5 units: `Network (files 1)`, `Reconnect`, `Server (files 1..3)`)
- `client/src/source/World/*` (7 units: `GameMaps (files 1..6)`, `MapInfra`)

## Key Rules & Invariants Verified
- **`Data/*`**:
  - `ItemDatabase.h`: Flat indexed item definition table (`Find`, `FindEntry`, `HasTag`, `IsAllowed`), returning `false` for all actions on unknown/invalid item types.
  - `GameConfig`: Client runtime configuration, validation, and personal tuning settings.
  - `DataHandler/*` & `GameData/*`: Encoded `.bmd` / `.dat` / table loaders and in-memory structures for items, monsters, skills, buffs, maps, looks, effects, sounds, and server lists.
- **`Network/*`**:
  - `SocketSystem.h` & `Network/Server/*`: Packet framing, encryption/modulus handling, socket option constants (`MAX_SOCKET_OPTION = 50`, `MAX_SPHERE_LEVEL = 5`), reconnect state, and server message dispatch.
- **`World/*`**:
  - `MapInfra/MapManager.h`: Canonical `ENUM_WORLD` map IDs (`WD_0LORENCIA` .. `WD_81KARUTAN2`, `SecondDevilSquareMapId = 32` mapped to `WD_9DEVILSQUARE`) and portal/map process management.
  - `GameMaps/*`: Per-map object, terrain, and event hooks.
- **Verdict**: No silent game rule regressions found across `Data`, `Network`, and `World`.
