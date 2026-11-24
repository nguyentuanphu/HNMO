# Unit: client/web/src/game (files 5)

Checked against: desktop `MoveCommandWindow`, `QuestMng.cpp`, `SkillManager`, and HNMO custom resets.

Scope: `client/web/src/game (files 5)` (`12` files, `3201` lines).

## Findings

| File & Line | Rule / Constant | Desktop / Suite Role & Value | Result |
|---|---|---|---|
| `warps.ts:41-55` | `warpLevel`: MG/DL/RF (`3,4,6`) use `floor(level * 2 / 3)` except Lv `400`; Icarus requires wings/Dinorant/Fenrir & forbids Uniria; Atlans forbids Uniria/Dinorant | Desktop `CMoveCommandWindow::SettingCanMoveMap` | Same |
| `quests.ts:37,47-54` | Max `10` concurrent quests; desktop request (`0..17`) & reward bitmask mapping | Desktop `CQuestMng` | Same |
| `skills.ts:12-85` | Reads `skill_eng.bmd` (`108`-byte record, `MAX_SKILLS = 650`) + HNMO `700..727` | Client skill table & HNMO custom skills | Same + HNMO custom |
| `resets.ts:13-96` | Reset / Relife / Delegated-reset packet UI adapter | HNMO custom reset system | HNMO custom |
