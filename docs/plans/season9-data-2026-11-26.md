# Season 9 Full Data & Systems Plan (2026-11-26)

> **Owner decision (26 Nov 2026, Q13):** *"Làm tất cả cùng lúc"* — deliver the complete Season 9 parity set across web client data and server configuration/logic, split into clean draft PRs into `claude/hnmo-main-handover-54co2m`, with everything tunable exposed in the Admin Panel in all four languages (`en`, `vi`, `de`, `zh-CN`).

## 1. Baseline & What Already Landed

The following Season 9 server data plug-ins (keyed to `VersionSeasonSix.DataInitialization.Id`, gated via `SeasonNineUpdatePlugInBase` / `HnmoSeasonInstance.IsSeasonNine`) are already on `claude/hnmo-main-handover-54co2m`:

- `SeasonNineUpdatePlugInBase`: executes only when `HNMO_SEASON=s9`; marks itself installed as a no-op on Season 6 so Season 6 data stays untouched.
- `SeasonNineSharedItemsUpdatePlugIn`: currently updates only Cape of Fighter (`12:49`), Cape of Lord (`13:30`), and basic healing/mana potions (`14:0..6`). Many shared item differences remain.
- `SeasonNineOnlyItemsUpdatePlugIn` + `SeasonNineOnlyItemsCatalog`: adds the 279 unique Season 9-only definitions (281 rows in the Season 9 server suite table; 264 new items on top of HNMO's base) with 4-language item names (`SeasonNineItemNames`). **Not yet mirrored into `Data/Items/` for the web/desktop clients.**
- `SeasonNineSocketOptionsUpdatePlugIn`: aligns Water shield protection (Lv 5 -> 30%) and Wind AG socket values.
- `SeasonNineMonstersUpdatePlugIn` + `SeasonNineMonstersCatalog`: adds 111 Season 9 monster/NPC definitions on the server and Dark Elf (`412`) respawn override; `client/web/public/data/Monsters/Season9.json` currently has only **35** entries (out of **615+** Season 9 monsters/NPCs on the server) and server shared-monster overrides (`diff-monsters.txt`) remain incomplete.
- `SeasonNineMapsAndGatesUpdatePlugIn` + `SeasonNineMapsCatalog`: adds Season 9 maps (`91, 92, 95, 96, 97, 98, 99, 100, 101, 102`), terrains, gates (`417..482`), and initial spawns; `SeasonData.IsSeasonNineOnlyMapFile` and `world.ts` route `/data/s9/` World/Object assets.
- `SeasonNineJinShopsUpdatePlugIn`: Jin merchants (`583`, `604`).

All remaining Season 9 work will ship in **three implementation draft PRs** (Parts **(a)**, **(b)**, and **(c)** below) derived directly from the Season 6 / Season 9 server suites without embedding suite names, proprietary binaries, or local filesystem paths.

---

## 2. Draft PR Sequence

### Part (a) — Items, Monsters & Core World Data Parity (Web + Server)

**Goal:** Align both `Data/Items/`, `Monsters/Season9.json`, and the server's Season 9 configuration behind `HnmoSeasonInstance.IsSeasonNine` / world season state.

1. **Shared Season 9 Item Overrides (Server + Season-Gated Web Overrides)**
   - **Server (`SeasonNineSharedItemsUpdatePlugIn` extension):** Cover the remaining shared item overrides between the Season 6 and Season 9 server suites (weapon/armor/shield reqs, drop levels, speeds, damage, durability, stackability, trade/sell flags — including Archangel weapons, Summon Spirit Stick `5:32`, Succubus set `7..11:53`, Sphere Tetra `12:73`, Reset Fruits `13:54..58`, Chaos Castle Ticket `13:121`, Combination Notes `14:194..203`, Boxes/Scrolls `14:206..234`, etc.), preserving any operator-customized values and leaving Season 6 unchanged.
   - **Web Client (`Data/Items/` + `client/web/src/game/items.ts`):** Introduce a season-scoped override feed/file (e.g., `Data/Items/Season9Overrides.json` and season-aware `/website/items` filtering) applied in `loadItemCatalog()` only when connected to a Season 9 world, so Season 6 tooltip/requirement/size behavior is never altered.
2. **Season 9-Only Items in `Data/Items/` (281 Suite Rows / 279 Unique Definitions)**
   - Add the Season 9-only definitions (weapons, shields, Bloodangel/Succubus/Socket/Dark/Holy armor sets, Wing/Cloak `12:262..267`, Combination Notes `14:191..203`, Pentagram/Muun/Mix/Event items `12:200..261`, `13:145..239`, `14:88..234`, `15:25..37`) into `client/src/bin/Data/Items/GroupNN_*.json` with 4-language `name` translations (`en`, `vi`, `de`, `zh-CN`) and corresponding model/glow entries in `Data/Items/Models/GroupNN_*.json` referencing `/data/s9/Item/*.bmd` (none of which collide with Season 6 item numbers).
3. **Web Monster Catalog (`client/web/public/data/Monsters/Season9.json`, 35 -> 615+ Parity)**
   - Expand `Monsters/Season9.json` from 35 entries to cover all Season 9 monster/NPC IDs, map variants, equipment/skin definitions, and `/data/s9/Monster/` + `/data/s9/NPC/` models (keeping Season 6 `Monsters.json` / `Models.json` first and cleanly separating genuinely model-less entries).
   - Add Season 9 shared monster stat overrides behind `SeasonNineUpdatePlugInBase` on the server where Season 9 suite differs from Season 6 (`diff-monsters.txt`).
4. **Admin Panel & Tests**
   - Expose Season 9 shared-item / monster sync toggles and `/item-definitions` season awareness in the Admin Panel with 4-language `.resx` captions (`en`, `vi`, `de`, `zh-CN`).
   - Add NUnit persistence tests (S9 updates applied vs operator customizations preserved vs S6 untouched) and Node unit tests validating every `Data/Items/*.json`, `Models/*.json`, and `Monsters/Season9.json` entry.

---

### Part (b) — Season 9 Events & Uruk Mountain (`669-729`)

**Goal:** Port all five Season 9 events and full Uruk Mountain (`669-729`) encounter logic with host-tunable Admin Panel configuration in 4 languages.

1. **Acheron Guardian (`AcheronGuardian` PlugIn)**
   - Scheduled obelisk defense event on Acheron (daily schedule, active days, warning/ready/battle phases, obelisk HP & waves, `Event_AcheronGuardian_Reward` drop bag) gated by Season 9 or Admin Panel toggle.
2. **Arca Battle (`ArcaBattle` PlugIn)**
   - Guild/Alliance elemental territory war across maps `92, 96, 101` (registration window, obelisk capture `598..602` & elemental aura spots `627..641`, elemental buffs, Trophy of Battle booty mix, winner buff/rewards).
3. **Chaos Castle Survival (`ChaosCastleSurvival` PlugIn)**
   - Tournament-cycle survival on map `97` (`24` configurable daily slots, min level `280`, min players `2`, survivor point/damage scoring, Final Award & consolation rewards).
4. **Tormented Square (`TormentedSquare` PlugIn)**
   - 4-player timed survival/boss arena on map `102` (`11` configurable daily slots, `30` monster wave rounds, extra-point boss rounds, tournament day range & reward tiers).
5. **Invasion Manager (`InvasionManager` PlugIn)**
   - Unified Season 9 timed invasion scheduler (configurable invasions across `19` daily time slots, map selection, announcement messages, group spawns including Moon Rabbit `674`, Pouch of Blessing `675`, Fire Flame Ghost `676`, Snakes `647..650`, Goats `726..729`, New Year Horses `691..694`, Golden Event bosses `652..657`, `677`).
6. **Uruk Mountain Monsters (`669-729`) & Map Spawns**
   - Verify & complete all `669..729` definitions, element attributes, AI/skills, boss respawn timers (Lord Silvester `#673`, Lord Elemental mobs `678..680`, Evomon `681`/`690`, Flame Trap `689`, NPCs `669`, `682..688`, Goats `726..729`), spawns on map `100`/`101`, and `Season9.json` 3D looks.
7. **Admin Panel & Tests**
   - Add an Admin Panel management section for Season 9 Events (enable/disable, schedule editor, level/player limits, durations, reward/drop multipliers, trigger-now buttons) with 4-language `.resx` resources (`en`, `vi`, `de`, `zh-CN`).

---

### Part (c) — Season 9 Systems (Muun, Pentagram, Skills `618-621`, Master Tree `330`, Minimap)

**Goal:** Implement the remaining Season 9 core gameplay systems on server and web client, behind season checks and host settings.

1. **Muun Pet System**
   - Sub-inventory / Muun equipment slots (`main` & `sub`), `IGC_MuunInfo` (`76` Muun entries), option scaling (`IGC_MuunOption`), Muun exchange (`IGC_MuunExchange`), evolution via Evolution Stone / Muun Energy Converter (`13:239`), server stat application & persistence, and web UI Muun window + in-world companion rendering.
2. **Pentagram / Elemental System**
   - Elemental affinity attribute (`Fire`, `Water`, `Earth`, `Wind`, `Darkness`), Pentagram equip slot (`236`) and Errtel socket slots (Anger, Blessing, Integrity, Divinity, Gale), `IGC_PentagramOption` / `IGC_PentagramJewelOptionValue` scaling, Errtel mix & extraction rates (`IGC_PentagramMixNeedSource`, `IGC_PentagramJewelOutRate`), elemental damage & resistance calculations (PvP/PvE elemental advantage matrix & monster elemental attributes in Acheron/Debenter/Uruk Mountain), and web tooltip/UI display.
3. **Four Season 9-Only Skills (`618-621`)**
   - `618 Spell of Protection`, `619 Spell of Restriction`, `620 Spell of Pursuit`, `621 Shield-Burn` — server skill & effect definitions, learn/cast rules, cooldown/duration/shield-burn mechanics, client/web skill catalog entries and cast animations/effects.
4. **330-Level Master Tree**
   - Season 9 Master Level cap `330` (vs Season 6 cap `200`) controlled by `HnmoSeasonInstance.IsSeasonNine` / tunable Admin Panel setting; extend `MasterSkillTree` node definitions (`293 -> 480` suite nodes across all 7 third-class trees including skill tier ranks, formulas, and missing passives) and update the web `MasterLevelWindow` grid/validation to render Season 9 nodes cleanly.
5. **Season 9 Minimap**
   - Port `IGC_MiniMap` (`256` markers) & battle-core minimap markers for Season 9 maps (`91, 92, 95, 96, 97, 98, 99, 100, 101, 102`) into web `MiniMap` (`client/web/src/ui/miniMap.ts`) and server/data definitions behind season gating.
6. **Admin Panel & Tests**
   - Host-tunable settings for Master Level max cap, Muun drop/exchange/evolution rates, Pentagram drop/mix/extraction multipliers, S9 skills enable/balance knobs, and Minimap visibility — all localized in `en`, `vi`, `de`, and `zh-CN` and verified by `AllCaptionsCanBeTranslatedAsync`.

---

## 3. Cross-Cutting Rules & Verification Standard

- **Season Isolation:** Season 6 (`HNMO_SEASON=s6`) behavior, item stats, master cap (`200`), skills, events, and web layouts must remain unchanged when Season 9 features are off.
- **Localization:** Every Admin Panel page, plug-in display caption, item/monster/skill/event label added must include `en`, `vi`, `de`, and `zh-CN` `.resx` entries; `AllCaptionsCanBeTranslatedAsync` must pass.
- **Safety & Confidentiality:** Never include third-party suite names, publisher names, local filesystem paths, or credentials.
- **Verification:** Each PR runs targeted NUnit tests (`HNMO.Persistence.Initialization.Tests`, `HNMO.Tests`, `HNMO.Web.Tests`), `npm run typecheck && npm run build` (reverting `world-editor.js` unless intentionally modified), and `node --test client/web/tests/*.test.mjs` through `heavy.ps1`.
