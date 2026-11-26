# Desktop Client Data Audit: Season 9 Items & Monsters (PR `#401` / `#419`)

Audited on `2026-11-27` (Sydney time) comparing `docs/audit/data/client-s9/items.csv` & `npcs.csv` (decoded from
Season 9 desktop `Data/Local/Eng/Item_eng.bmd` and `Npcname_eng.bmd`) against `origin/integration/s9`
(`client/src/bin/Data/Items/Group*.json`, `client/web/public/data/Items/Season9Overrides.json`,
`client/src/bin/Data/Monsters/Monsters.json`, `client/web/public/data/Monsters/Season9.json`) and
`docs/audit/data/hnmo-s9/`.

## Items Summary

- **Counts:** Season 9 desktop `Item_eng.bmd` = **`1,252`** items (`client-s9/items.csv`); `integration/s9` web
  (`Group*.json` = `1,265`, `Season9Overrides.json` = `210` overrides); `hnmo-s9/items.csv` = **`1,112`** server items.
- **Coverage vs Client:** **`0` missing** (`1,252 / 1,252` Season 9 desktop client items exist in `integration/s9`
  web `Group*.json` + `Season9Overrides.json`). **`0` width × height mismatches** between `Item_eng.bmd` and
  `integration/s9` web item grids.
- **Missing from Web vs `hnmo-s9` server (`14` items):** `(0:38) Frick Large Glove`, `(7..11:77) Bloody Dragon`
  5-piece set, `(13:162) Fighter Amulet`, `(13:166) Brown Panda Transformation Ring`, `(13:168) Gold Character Card`,
  `(14:172..175) Magic Backpack 1..4`, `(14:203) Greater Combination Note`. These `14` IDs are **not** present in
  Season 9 desktop `Item_eng.bmd` either (they came from `SeasonNineExtendedSharedItemsUpdatePlugIn` / server catalog),
  so the web catalog matches the Season 9 desktop `Item_eng.bmd` completely while missing those `14` server-only IDs.
- **Items in Client `Item_eng.bmd` absent from `hnmo-s9/items.csv` (`159` items):** `-J` rental weapons/armors
  (`0:39`, `0:40`, `2:21`, `4:27`, `5:38..40`, `6:27..28`, `7..11:78..83`), `Succubus` set (`7..11:53`), Cash Shop /
  Event tickets / Lucky items (`13:59..134`, `14:125..148`).
- **Value differences (`63` items in `diff-client-s9.txt`):**
  - Scroll/Orb `reqEne` / `reqStr` / `reqAgi`: desktop `.bmd` stores unscaled base stat requirements (`reqEne=100`
    while server `items.csv` uses folded/effective formula values e.g. `140..1058`, matching existing `diff-skills.txt`).
  - `Sacred Fire / Storm Jahad / Piercing Groove` (`7..9,11:59..61`): desktop `reqLvl = 0`, server `1`.
  - `(13:50) Illusion Sorcerer Covenant`: client `1x2` vs server `2x2`; `(13:51) Scroll of Blood`: client `2x2` vs server `1x2`.
  - `(13:10) Transformation Ring`: client `reqLvl=0` vs server `50`; `(13:14) Loch's Feather`: client `reqLvl=78` vs server `0`.

## Monsters & NPCs Summary

- **Counts:** Season 9 desktop `Npcname_eng.bmd` = **`651`** entries; `integration/s9` web merged (`Monsters.json` `575`
  + `Season9.json` `82`) = **`657`** unique monster/NPC numbers; `hnmo-s9/monsters.csv` = **`615`**.
- **Coverage vs Client:** **`0` missing** (`651 / 651` Season 9 desktop `Npcname_eng.bmd` entries are covered by
  `integration/s9` web `Monsters.json + Season9.json`).
- **Server-only IDs (`2`):** `616` (`Brown Panda Transformation Ring`) and `617` (`Pink Panda Transformation Ring`) in
  `hnmo-s9/monsters.csv` are transform model dummy entries absent from both `Npcname_eng.bmd` and web `Season9.json`.
- **Client IDs absent from `hnmo-s9/monsters.csv` (`39`):** Event / Golden Invasion / Transformed NPCs (`55, 56, 68,
  104, 234, 258, 296, 300..303, 365, 373, 374, 400..403, 413, 414, 463..466, 493..503, 548, 560, 561, 567`).
