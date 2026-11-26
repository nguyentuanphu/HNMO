# Unit Audit: `client/src/source/GameLogic/Quests`

- **Unit**: `client/src/source/GameLogic/Quests`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `8` (`3,267` lines) including `CSQuest.cpp`, `QuestMng.cpp`, `DialogStructure.h`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client Value / Formula | Server / Suite Reference | Match |
|---|---|---|---|---|
| Legacy class-change quest 2-bit bitset packing | `CSQuest.cpp:50-80,290` | Packs `4` quest states per byte (`QUEST_STATE_BIT_WIDTH = 2`, mask `0x03`, states `NONE/ING/END/NO`); tracks `5` monster kill counters (`QUEST_MONSTER_SLOT_COUNT = 5`, stride `2`) | Web `quests.ts` & Server `LegacyQuestState` | Same |
| Quest item level requirement match | `CSQuest.cpp:713` | `nLevel == -1 || nLevel == pItem->Level` checks exact item level or wildcard `-1` in bag | Web `quests.ts` & Server `QuestProgress` | Same |
| New quest (`0xF6`) state & reward table | `QuestMng.cpp:156-180,292` | Stores `QuestByEtc`, progress scripts, requirements (`Item`/`Monster`/`Skill`/`Level`), and rewards (`EXP`/`Zen`/`Item`/`Buff`/`Contribution`) | Server `Quests` actions | Same |
