# Unit Audit: `client/src/source/UI/Quests`

- **Unit**: `client/src/source/UI/Quests`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `14` (`2,684` lines) including `MyQuestInfoWindow.cpp`, `QuestProgress.cpp`, `QuestProgressByEtc.cpp`
- **Status**: `Checked` (reclassified from `"No game rules"` — `MyQuestInfoWindow.cpp` hardcodes Blood Castle & Illusion Temple daily entry limit display)

## Rules & Behavior Checked (`file:line`)

| Rule / Check | File:Line | Client Value / Behaviour | Server / Suite Reference | Match |
|---|---|---|---|---|
| Hardcoded daily event entry cap display | `MyQuestInfoWindow.cpp:545-557` | Displays remaining `GetEventCount(2)` (BC) and `GetEventCount(3)` (IT) against a hardcoded limit of `6` (`YouMayEnterOnlyDTimesPerDay, 6`) | Server `MiniGameConfiguration` daily entry cap (can exceed or differ from `6` when customized) | **Differs** if server customizes daily entry limit `!= 6` |
| Quest summary & condition views | `QuestProgress.cpp`, `QuestProgressByEtc.cpp`, remaining files | Delegates condition evaluation & state transitions to `QuestLog` (`GameLogic/Quests`) | Audited in `GameLogic/Quests` | Same |
