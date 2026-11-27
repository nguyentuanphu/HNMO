# Desktop Client Data Audit: Season 6.3 Parity vs HNMO Server & Web

Audited on `2026-11-27` (Sydney time) from the Season 6.3 desktop client `Data` pack using
`tools/local-checks/desktop-data/` (`docs/audit/data/client-s6/*.csv` and `docs/audit/data/diff-client-s6.txt`).

## 1. Skills & Master Skill Tree (`skill_eng.bmd`, `MasterSkillTreeData.bmd`, `MasterSkillTooltip_eng.bmd`)

- **Skills (`399` non-empty in S6 `skill_eng.bmd` vs `316` in `hnmo/skills.csv`):**
  - `274` match on mana, AG, and range; `117` passive/strengthener master skills (`314–324` covered in PR `#411` +
    class rank passives `339–617`) are absent from `hnmo/skills.csv`.
  - **`8` active/master skills differ:** `13` Cometfall (`range: 3 vs 6`), `40` Nova (`mana: 180 vs 15`),
    `263` Dark Side (`range: 5 vs 4`), `336` Slash Strengthener (`range: 3 vs 2`), `382`/`484` Blast Strengthener
    (`range: 3 vs 6`), `558` Chain Drive Strengthener (`ag: 22 vs 20`), `559` Dark Side Strengthener (`range: 5 vs 4`).
- **Master Skill Tree (`MasterSkillTreeData.bmd`, `293` rows, skills `300–608`):**
  - `292 / 293` match `maxLevel` in HNMO.
  - **`1` differs:** **`571` (`Equipped Weapon Mastery`, record `285`, `classCode = 64` RF/FM)** has
    **`maxLevel = 10`** in both S6.3 & S9 `MasterSkillTreeData.bmd` and `"skill level %d/10"` in
    `MasterSkillTooltip_eng.bmd[253]`, vs `20` in `hnmo/skills.csv` (PR `#424` aligns S9 master tree to `10`).

## 2. Buffs (`BuffEffect_eng.bmd`, `158` rows)

- `158 / 158` match `client/src/bin/Data/Buffs/Buffs.json` (`0` missing).

## 3. Items, Ancient Sets, Socket Options, Harmony Options (`client-s6/*.csv`)

- **Items (`967` non-empty in S6 `Item_eng.bmd` vs `848` in `hnmo/items.csv`):**
  - `769` match size + stat/level requirements.
  - `64` differ: Skill Scrolls/Orbs store base `reqEne/Str/Agi` in `.bmd` vs server folded formula; `(13:50)`
    Illusion Sorcerer Covenant is `1x2` (client) vs `2x2` (HNMO); `(13:51)` Scroll of Blood is `2x2` (client) vs
    `1x2` (HNMO); `(13:10)` Transformation Ring `reqLvl: 0 vs 50`; `(13:14)` Loch's Feather `reqLvl: 78 vs 0`;
    `(7..9,11:59..61)` RF sets `reqLvl: 0 vs 1`.
  - `134` missing from `hnmo/items.csv`: Cash Shop / Event tickets / Chaos Cards `14:54/92/93/95` (addressed in
    PR `#405`) / `Pearl` set (`7..11:53`).
- **Ancient Sets (`itemsetoption_eng.bmd`, `49` set entries; `itemsettype.bmd`, `168` ancient item mappings):**
  Matches the `49` sets (`437` flattened option rows in `hnmo/ancient_sets.csv`).
- **Socket Options (`SocketItem_eng.bmd`, `35` rows):** `35 / 35` count match (`hnmo/socket_options.csv` = `35`).
- **Harmony Options (`JewelOfHarmonyOption_eng.bmd`, `26` rows):** `26 / 26` count match (`hnmo/item_options.csv`
  has `26` Harmony options).
- **ItemAddOption (`ItemAddOption.bmd`, `101` rows):** Decoded to `client-s6/item_add_options.csv`.

## 4. Maps, Gates, Warps (`movereq_eng.bmd`, `Gate.bmd`)

- **Warps (`39` in S6 `movereq_eng.bmd` vs `40` in `hnmo/warps.csv`):**
  - `25` match; **`14` differ or are missing in HNMO**: HNMO keeps lower custom warp Zen (`8,500..15,000` vs S6
    `50,000..1,000,000`) and lower level requirements on `KanturuRuins1..3` (`160..230` vs `200..300`),
    `Vulcanus` (`30` vs `250`), `Crywolf` (`280` vs `300`); warp `49` (`Stadium`) is absent from `hnmo/warps.csv`.
  - In Season 9 (`client-s9/warps.csv`), all `37 / 37` client warps match `hnmo-s9/warps.csv`.
- **Gates (`339` non-zero in S6 `Gate.bmd`, `438` in S9 `Gate.bmd`):** Decoded to `client-s6/gates.csv` &
  `client-s9/gates.csv` (`enter=218 / exit=199` split in `hnmo/*.csv`).

## 5. NPC Shops, Chaos Mixes, Events

- **Mixes (`mix.bmd`, `101` recipes in S6; `Mix.bmd`, `159` recipes in S9 vs `43` in `hnmo/mixes.csv`):**
  Chaos Machine client recipes (`101` S6 / `159` S9 across `14` mix types) decoded to `client-s6/mixes.csv`.
- **NPC Shops:** Client `Data/` does **not** hold standard NPC shop inventories (server-only `shops.csv`); it
  only holds Cash Shop `InGameShopScript` (`235` packages, `2,189` products in S6; `161` packages, `715` products in S9).
- **Events:** Client `Data/` holds event entry UI strings (`Text_eng.bmd`), buffs (`BuffEffect_eng.bmd`), and gate
  triggers (`Gate.bmd`), while event schedules, wave counts, and drop bags live on the server (`events.csv`)
  — covered in PR `#422`.
