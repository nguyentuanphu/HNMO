# Unit: client/src/source/GameLogic/Quests

Checked against: Server legacy quest wire state & Web `quests.ts`.

Scope: `client/src/source/GameLogic/Quests` (`8` files, `3267` lines).

## Findings

| File & Line | Rule / Constant | Parity Reference | Result |
|---|---|---|---|
| `CSQuest.cpp:50-55` | 2-bit packed quest states (`4` per byte), `QUEST_MONSTER_SLOT_COUNT = 5` | Classic MU legacy quest state bitmask & Web `quests.ts` | Same |
