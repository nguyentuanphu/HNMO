# Unit: client/web/src/game (files 4)

Checked against: desktop `MasterSkillTreeLayout.cpp`, `MixMgr.cpp`, and server `GameConfiguration`.

Scope: `client/web/src/game (files 4)` (`15` files, `5819` lines).

## Findings

| File & Line | Rule / Constant | Desktop / Server Role & Value | Result |
|---|---|---|---|
| `masterLevel.ts:18-24,103-127,180-182` | `MASTER_SKILL_BEGIN = 300..608`, `MASTER_EXPERIENCE_UNLOCK_LEVEL = 400`, `masterLevelStart` polynomial `((9+total)*total^2*10 + (9+over)*over^2*1000 - 3892250000)/2` | Desktop `MasterSkillTreeLayout.cpp:14-40` & server `MasterExperienceFormula` | Same |
| `mixRecipes.ts:13-44,64-89` | `14` `MixType` recipes (`mix.bmd`), `MIX_STORAGE`, `SP` flags, `OP` rate evaluator | Desktop `GameLogic/Items/MixMgr.cpp` | Same |
| `newSkills.ts:13-22` | Custom HNMO skills `700..727` visuals & icons mapped from `public/data/new-skills.json` | HNMO custom skills | HNMO custom |
