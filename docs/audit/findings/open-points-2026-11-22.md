# Open points from the Season 6 and Season 9 audit (22 Nov 2026, Sydney time)

Notes recorded for the owner and Main Claude at the close of the local audit and Season 9 data handover.

## 1. The Arena map (`6`) layout (client and server)

- Both `client/src/bin/Data/World7/` (`Object7/`) and `server/src/Persistence/Initialization/Resources/Terrains/Season6/Terrain7.att` carry the older Arena layout:
  - `World7/EncTerrain7.obj` in the base data is `44,674` bytes, whereas the expanded Arena layout in the Season 6 and Season 9 client data is `155 KB` and adds 35 object models (`Object7/Object42.bmd` through `Object76.bmd`), updated terrain attributes/lights, and `World7/mini_map.ozt`.
  - Several default gates and cage spots on map `6` overlap blocked tiles in the older `Terrain7.att`, which is why the HNMO Arena program uses its own walkable gate rectangle (`X: 95..100, Y: 105..110`, configured via `ArenaConfiguration.Home*`).
- **Action needed when upgrading map `6`:** client and server must be updated together in a single change (`client/src/bin/Data/World7/`, `client/src/bin/Data/Object7/`, `server/src/Persistence/Initialization/Resources/Terrains/Season6/Terrain7.att`, the monster spawns in `server/src/Persistence/Initialization/Version075/Maps/Arena.cs`, and `ArenaConfiguration.Home*`). Updating only one side causes walkability desynchronization (`NOMOVE` rejections and safe-zone warps).

## 2. Five Season 9 monsters and NPCs without a 3D model (`580, 581, 584, 643, 689`)

The server definitions and spawns for all 111 Season 9 monsters and NPCs are registered in `SeasonNineMonstersCatalog.cs` and `SeasonNineMapsCatalog.cs`, and the web client maps the 96 new Season 9 field/boss/NPC models in `client/web/src/season9Monsters.ts`. Five Season 9 type numbers still have no known `.bmd` model mapping in the client data and therefore fall back to the placeholder ghost model (`Monster/Monster08.bmd`) when spawned:

| Number | Name | Kind / Map | Status |
|---:|---|---|---|
| `580` | Captain Slough | Monster (Acheron / Alkmar `91`) | No model file identified in `Data_S9/Monster/`; uses fallback model |
| `581` | Deruvish | Monster (Acheron / Alkmar `91`) | No model file identified in `Data_S9/Monster/`; uses fallback model |
| `584` | Sir Lesnar | NPC (Loren Market `79` at `126, 106`; Acheron `91` at `32, 210` and `58, 186`) | No model file identified in `Data_S9/NPC/`; uses fallback model |
| `643` | Mait | NPC (Loren Market `79` at `135, 134`) | No model file identified in `Data_S9/NPC/`; uses fallback model |
| `689` | Flame Trap | Event trap object (`Level 10`, non-combat trap entry in the Season 9 monster table) | Trap entity rather than a standard monster mesh; not spawned on normal hunting maps |

## 3. Legacy reward bags of events HNMO lacks

The server importer (`server/src/GameLogic/ServerImporter/LegacyServerImportEngine.cs`) imports:
- per-monster drop bags (`Monster_(N)`),
- box drop bags (`Item_(group,number,level)`), and
- Blood Castle / Chaos Castle winner reward bags (`Event_BloodCastle(1..8)_Reward`, `Event_ChaosCastle(1..7)_Reward`).

The remaining `ItemBags/*.xml` files in the Season 6 and Season 9 server suites belong to events, mini-games, or NPC exchanges that have no reward target in HNMO today. They are surfaced in the importer preview list and intentionally skipped during import:

- **Illusion Temple reward bags:** `Event_IllusionTemple(1..6)_Reward` and Season 9 `Event_IllusionTemple_Renewal_*` (HNMO's Illusion Temple awards experience/score rather than per-tier item bags).
- **Last Man Standing:** `Event_LastManStanding_Reward`.
- **Mu Rummy:** `Event_MuRummy_Reward_1..3`.
- **Lucky Coin exchange (Delgado):** `NPC_Delgado_LuckyCoin(10)_Reward`, `NPC_Delgado_LuckyCoin(20)_Reward`, `NPC_Delgado_LuckyCoin(30)_Reward`.
- **Cherry Blossom mix (Cherry Blossom Spirit):** `Mix_CherryBlossom_White_Reward`, `Mix_CherryBlossom_Red_Reward`, `Mix_CherryBlossom_Gold_Reward`.
- **Santa Village / Santa Claus:** `NPC_SantaClaus_Reward` and seasonal gift bags.
