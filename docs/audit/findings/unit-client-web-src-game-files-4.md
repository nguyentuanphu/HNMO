# Unit Audit: `client/web/src/game (files 4)`

- **Unit**: `client/web/src/game (files 4)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `15` (`5,819` lines): `legacyQuestDialog.generated.ts`, `masterLevel.ts`, `messenger.ts`, `miniMapData.ts`, `mixRecipes.ts`, `monsterData.ts`, `movementRecorder.ts`, `newSkills.ts`, `objectFires.ts`, `online.ts`, `party.ts`, `pathfinding.ts`, `perfRecorder.ts`, `pets.ts`, `prewarm.ts`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Desktop / Server / Suite Reference | Match |
|---|---|---|---|---|
| Master Level unlock, tree rank rules & class codes | `masterLevel.ts:15-25,73-110+` | `MASTER_SKILL_BEGIN = 300`, `END = 608`, `3` categories, `MASTER_SKILL_LEVEL_REQ_FOR_NEXT_RANK = 10`, `MAX_MASTER_TREE_RANK = 10`, `MASTER_EXPERIENCE_UNLOCK_LEVEL = 400`; parses `MasterSkillTreeData.bmd` (`24` B xor) and maps Master classes `3/7/11/13/17/23` | Desktop `MasterLevel` & `common-server-and-character-rules-s6-s9.md` | Same |
| Chaos Machine recipes, rate expression & item flags | `mixRecipes.ts:13-64+` | `MIX_TYPE_COUNT = 14`, `MIX_STORAGE` mapping; evaluates recipe success formula opcodes (`ADD/SUB/MUL/DIV/INT/MAXRATE`), special option bitmasks (`EXCELLENT=1, ADD380=2, SET=4, HARMONY=8, SOCKET=16`), item counts & charm bonuses | Desktop `MixMgr` & `ChaosMachineMix` (`mix.bmd`) | Same |
| Friend mail limits & server sentinels | `messenger.ts:10-30` | `LETTER_COST = 1000`, `MAX_LETTER_TEXT = 1000`, `MAX_SUBJECT_LENGTH = 32`, `LAST_ONLINE_SERVER = 0xfc`, `UNAVAILABLE_SERVER = 0xfd` | Desktop `CUILetterWriteWindow` & `FriendServer` | Same |
| Party member cap & facing helper | `party.ts:16-75` | Clamps party size at `5` (`Math.min(5, bytes[4])`), decodes 24-byte member records, finds nearby facing target | Server `PartyManager` (`MaxPartySize = 5`) | Same |
| Mounts & companion equipment rules | `pets.ts:22-68` | `TICK_SECONDS = 1/25`, maps `Dark Horse/Uniria/Dinorant/Fenrir(Red/Black/Blue/Gold)`, cash-shop pets (`Demon`, `Guardian`, `Rudolf`, `Panda`, `Unicorn`, `Skeleton`), checks SafeZone/Atlans/Icarus mount behavior | Desktop `CSPetSystem` & `ZzzCharacter` | Same |
| Walk steps limit, NPC talk range & rubberband resume | `online.ts:16-28` | `ID_MASK = 0x7fff`, `APPEARANCE_LENGTH = 27`, `MAX_STEPS_PER_WALK = 15`, `TALK_DISTANCE = 3` cells, `PING_INTERVAL_MS = 20000` | Server `WalkAction` & `TalkToNpcAction` | Same |
| Custom HNMO skills (`700..727`) | `newSkills.ts:13-25,87-166` | Defines custom HNMO skills `700..727` per class from `/data/new-skills.json` | HNMO server skill additions (`Persistence/Initialization`) | HNMO custom |
| A* pathfinding walkable flags | `pathfinding.ts:19-40` | Blocks `TerrainFlag.NOMOVE \| TerrainFlag.NOGROUND`, `MAX_SEARCHED_CELLS = 4000`, `DIAGONAL_COST = 1.4142` | Desktop `PathFinding` / Server `Pathfinding` | Same |
| Static generated quest text, minimap markers & telemetry | `legacyQuestDialog.generated.ts`, `miniMapData.ts`, `monsterData.ts`, `movementRecorder.ts`, `objectFires.ts`, `perfRecorder.ts`, `prewarm.ts` | Generated NPC dialog text, BUX-encoded minimap markers, monster animation/setup table, torch fires, telemetry & texture prewarm | Data / presentation / diagnostics | No game rules |
