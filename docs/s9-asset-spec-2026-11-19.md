# Season 9 Client Asset Spec (Wave 4A)

- **Date:** 2026-11-19 (Australia/Sydney)
- **Scope:** Local Windows inspection of Season 9 client `Data/` assets (`Data_S9` overlay; zero proprietary files committed to git) + HNMO web/bridge decoders implemented in Wave 4A.

## 1. Format Compatibility Summary

Inspection across all extracted Season 9 client `Data/` files (`122` top-level directories) shows every runtime 3D asset falls into one of four categories:

| Asset / Extension | S9 Count | Container / Header | Wave 4A Support |
| --- | ---: | --- | --- |
| `*.bmd` (`0x0e`) | 3,578 | `"BMD\x0e"` + `int32` size + 2-stage `ModulusCipher` (`34`-byte header/key + `0x0a`-compatible body) | **Implemented** in `client/web/src/modulusCipher.ts` & `client/web/src/formats.ts` (`3,578 / 3,578` verified) |
| `*.bmd` (`0x0c` / `0x0a`) | 2,782 | Existing `MapFileDecrypt` / plain `BMD` body | Existing in `formats.ts` |
| `EncTerrain*.map` (`"MAP\x01"`) | 8 extra worlds | `"MAP\x01"` + 2-stage `ModulusCipher` (`196,648` bytes total -> `196,610`-byte mapping) | **Implemented** in `formats.ts:parseTerrainMapping` (`8 / 8` verified) |
| `EncTerrain*.att` (`"ATT\x01"`) | 8 extra worlds | `"ATT\x01"` + 2-stage `ModulusCipher` + `BuxConvert` (`131,114` bytes total -> `131,076`-byte wide `WORD` attributes) | **Implemented** in `formats.ts:parseTerrainAttributes` (`8 / 8` verified) |
| `EncTerrain*.obj` (`version >= 1`) | `World101` (`8e`) | `MapFileDecrypt` header `version = 1`, `32`-byte object records (`[int16 type, Vec3 pos, Vec3 angle, float scale, uint16 tail]`) instead of v0 `30`-byte records | **Implemented** in `formats.ts:parseWorldObjects` |
| `TerrainHeight.OZB` | All 8 extra worlds | `196,666` bytes (24-bit extended height in `World83`, `World101`) or `66,620` bytes (standard 8-bit) | Already supported in `formats.ts:parseTerrainHeight` |
| `*.ozj` / `*.ozt` | 7,863 / 2,103 | Standard `24`-byte JPEG / `4`-byte TGA prefix (identical to S6) | Existing in `GameFiles.cs` & `convert-textures-ktx2.mjs` |
| `*.ozp` | 45 | Only in `Interface/GFx/NaviMap/` — `4`-byte duplicate `\x89PNG` prefix before valid PNG | **Implemented** in `GameFiles.cs` (`.png` -> `.ozp` fallback, `OzpPrefixBytes = 4`, `image/png`) |
| `*.ozd` / `*.ozg` | 981 / 96 | Scaleform GFx UI assets, located **strictly** under `Interface/GFx/` (`0` elsewhere) | Not needed by HNMO HTML/Canvas web UI |
| `*.wav` / `*.mp3` | 1,013 / 47 | Standard audio | Existing in `GameFiles.cs` & `package-hnmo.sh` (`opus` conversion) |

### ModulusCipher Details (`client/web/src/modulusCipher.ts`)
- Mirrors `client/src/source/Render/Models/BmdModulusCipher.cpp` across all 8 block ciphers (`algorithmByte & 0x07`):
  - `0`: TEA (8-byte BE, 32 rounds)
  - `1`: ThreeWay (12-byte BE key / LE block, 11 rounds)
  - `2`: CAST-128 (8-byte BE, 16 rounds)
  - `3`: RC5 (8-byte LE, 16 rounds)
  - `4`: RC6 (16-byte LE, 20 rounds)
  - `5`: MARS (16-byte LE, 8+16+8 rounds)
  - `6`: IDEA (8-byte BE, 8 rounds)
  - `7`: GOST 28147-89 (8-byte LE, 32-byte key)
- All 64 `(stage1, stage2)` cipher selector combinations appear in S9 client assets and were verified against all 3,578 `BMD:0x0e` models and all 16 S9 `"MAP\x01"` / `"ATT\x01"` files.

## 2. Season 9 World Folders & Map IDs (`Data/WorldN`, `Data/ObjectN`)

HNMO maps server `mapNumber` to `World{mapNumber + 1}` / `Object{mapNumber + 1}` (`client/web/src/world.ts:filesFolder`). Relative to S6 `client/src/bin/Data`, S9 adds:

| Folder | Server Map ID | Notes from Extracted Files |
| --- | ---: | --- |
| `World83` / `Object83` | `82` | `TerrainHeight.OZB` 24-bit (`196,666` B), `EncTerrain83.map` (`MAP\x01`), `.att` (`ATT\x01`), `.obj` v0 (`804` objects); `Terrain82.att` already exists on server |
| `World92` / `Object92` | `91` | `8`-bit `TerrainHeight.OZB` (`66,620` B), `EncTerrain92.map` (`MAP\x01`), `.att` (`ATT\x01`), `.obj` v0 (`5,844` objects), `AlphaTile01.OZT`, `Object92/` (`117` BMDs) |
| `World94` / `Object94` | `93` | Includes `Camera_Angle_Position.bmd`, `EncTerrain94.*` (`MAP\x01`/`ATT\x01`/v0 `.obj`) |
| `World95` / `Object95` | `94` | Includes `Camera_Angle_Position.bmd`, `EncTerrain95.*` (`MAP\x01`/`ATT\x01`/v0 `.obj`) |
| `World96` / `Object96` | `95` | Includes `ExtTile01..06.OZJ`, `Fog.OZJ`, `rain*.OZT`, `leaf*.OZJ/.OZT`, `EncTerrain96.*` (`MAP\x01`/`ATT\x01`/v0 `.obj`) |
| `World99` / `Object99` | `98` | `AlphaTileGround03.OZT`, `EncTerrain99.*` (`MAP\x01`/`ATT\x01`/v0 `.obj`) |
| `World101` / `Object101` | `100` | `24`-bit `TerrainHeight.OZB` (`196,666` B), `TerrainServer.att` (`65,539` B), `EncTerrain101.obj` **v1 (`3,482` x `32`-byte records)** |
| `World103` / `Object103` | `102` | `8`-bit `TerrainHeight.OZB` (`66,620` B), `rain*.OZT`, `leaf02.OZJ`, `EncTerrain103.*` (`MAP\x01`/`ATT\x01`/v0 `.obj`) |

Additional extra S9 client top-level asset counts vs. S6:
- `Item/`: `1,651` files (`+698` files vs S6 — wings, shields, evolution/muun pets, sets, jewels/pentagram items)
- `Monster/`: `1,261` files (`+314` files vs S6 — `monster218`+)
- `Skill/`: `403` files (`+37` files vs S6 — Grow Lancer / RF / ELF skills & arrows)
- `Effect/`: `396` files (`+76` files vs S6)
- `Music/`: adds `Acheron.mp3`, `Urke_Clean_Area.mp3`, `Urke_Fire_Area.mp3`

## 3. Data_S9 Optional Overlay Wiring

- `GameFiles` (`client/web/bridge/GameFiles.cs`) accepts an optional `GameFiles? overlay` instance: overlay files (loose or inside `overlay/packs`) take precedence while missing paths cleanly fall back to base `Data/`.
- `Program.cs` (`HNMO:S9DataPath` configuration key, or sibling `Data_S9` folder next to resolved `DataPath` if present) constructs the overlay automatically with zero impact when `Data_S9` / `HNMO:S9DataPath` does not exist.
- `client/web/tools/package-hnmo.sh` honors optional `HNMO_S9_DATA_DIR` during data packing: files overlay `$OUT/Data` before `convert_audio`, `build-allowlist.mjs`, `convert-textures-ktx2.mjs`, and `HNMO.Web --pack` run. Because `build-allowlist.mjs` uses `formats.js:parseBMD` and `parseWorldObjects`, S9 `0x0e` models and v1 world objects automatically pull their texture dependencies when `Data_S9` is supplied.

## 4. Handoff Notes for Cloud Claude (Waves 4B–4D)

1. **`client/web/src/world.ts`**: Register S9 maps (`82, 91, 93, 94, 95, 98, 100, 102` and any S9 instance/shared-folder aliases) in `MAP_NAMES` / `SHARED_FOLDERS` / `EVENT_MAP_NAMES`, plus load any per-world `ExtTile01..06` / fog behavior if desired. Guard missing asset files gracefully if running against S6-only `Data/`.
2. **Server & Packets (Waves 4B–4D)**: Extend server packet handlers, persistence/game definitions, Elemental/Pentagram/Ertel, Muun pets, Grow Lancer, and Acheron/Debenter/Urk/Tormented Square maps/events. Keep all new `F5` sub-opcodes strictly within free slots (`2B–2F`, `4E–4F`, `50–6F`). Keep EN + VI strings in all `.resx` / UI files (`.resx` append-only).
