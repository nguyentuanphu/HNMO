# Parser Findings: Client Encoded & Web Data Tables (`diff-client-tables.txt`)

- **Checked on**: 2026-11-24
- **Outputs**: [diff-client-tables.txt](../data/diff-client-tables.txt)
- **Sources**: the Season 6 client's decoded `.bmd`/text tables, the Season 9 client's decoded `.bmd`/text tables, HNMO server CSVs (`docs/audit/data/hnmo/` & `docs/audit/data/hnmo-s9/`), and HNMO client/web data (`client/src/bin/Data/` & `client/web/public/data/`)

## Fixes Applied

1. **Multi-source comparison**: Extended `tools/audit/suite-parsers/client-tables.mjs` to compare all decoded client tables against BOTH HNMO's server dumps and HNMO's shared desktop/web runtime tables (`Data/Items/Group*.json`, `Data/Buffs/Buffs.json`, `Data/gate.bmd`, `Data/Local/Eng/MoveReq_eng.bmd`, `Data/Local/mix.bmd`, `Data/Local/Eng/skill_eng.bmd`, `Data/Local/Eng/itemsetoption_eng.bmd`, `Data/Local/Eng/socketitem_eng.bmd`, `Data/Local/Eng/JewelOfHarmonyOption_eng.bmd`, `Data/Local/MasterSkillTreeData.bmd`, `Quest_eng.bmd`, `QuestProgress.bmd`, `NPCDialogue.bmd`, `QuestWords_eng.bmd`, `client/web/public/data/new-skills.json`, and `Monsters/Season9.json`).
2. **Decoder fixes**:
   - `decodeMoveReq`: Fixed 84-byte record offsets (`reqLevel@68`, `maxLevel@72`, `zen@76`, `gate@80`, matching `client/web/src/game/warps.ts`) instead of the shifted 80-byte offsets.
   - `decodeSkillBmdNames`: Added the 108-byte record stride (`650 * 108 + 4 = 70,204` bytes) used by HNMO's `skill_eng.bmd` (`399` skills, matching `client/web/src/game/skills.ts`).
   - `decodeBuffEffect`: Read strings cleanly after the 5-byte prefix and matched `b.index` to `Buffs.json` `b.number`.
3. **Removed connection/infra tables**: Dropped `ServerInfo.bmd` and `ServerList.bmd` from game-rule diffs.

## Hand-Verified Sample Values

1. **Warps (`MoveReq_eng.bmd` & `warps.ts` vs `warps.csv`)**:
   - Hand check in `client/web/src/game/warps.ts:11-23` and decoded `movereq_eng.bmd`: Warp `#1` (`Arena`) has `reqLevel = 50`, `maxLevel = 400`, `zen = 2000`, `gate = 50`; Warp `#25` (`Aida1`) has `reqLevel = 150`, `maxLevel = 400`, `zen = 50000` in the Season 6 client vs `zen = 8500` in `HNMO_Client` and `warps.csv`.
2. **Skills (`skill_eng.bmd`, `new-skills.json` vs `skills.csv`)**:
   - `S6_Client = 399`, `S9_Client = 403` (`+4` S9-only `#618..621`), `HNMO_Client = 399` (`70,204` bytes), `Web_new_skills = 28` (`700..727`), `HNMO_Srv = 316`.
3. **Buffs (`BuffEffect_eng.bmd` vs `Buffs.json`)**:
   - Hand check of entry `1` in `BuffEffect_eng.bmd` (`Increase Attack Power`) and `Buffs.json:5-10` (`number: 1`, `"Increase Attack Power"`) -> `158 / 158` Season 6 buffs matched (`+44` S9-only buffs `#122..186+`).
