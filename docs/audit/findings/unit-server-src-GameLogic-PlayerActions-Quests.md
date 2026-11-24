# Audit Findings: `server/src/GameLogic/PlayerActions/Quests`

- **Unit**: `server/src/GameLogic/PlayerActions/Quests` (8 files, 620 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference saved**: [quests-and-class-progression-s6-s9.md](file:///D:/WorkSpace/HNMO-ag/docs/audit/references/quests-and-class-progression-s6-s9.md)

## Files Reviewed (in full)
1. `NpcBuffRequestAction.cs` (1–97)
2. `PlayerQuestExtensions.cs` (1–83)
3. `QuestCancelAction.cs` (1–32)
4. `QuestClientAction.cs` (1–31)
5. `QuestCompletionAction.cs` (1–215)
6. `QuestExtensions.cs` (1–30)
7. `QuestSelectAction.cs` (1–56)
8. `QuestStartAction.cs` (1–75)

## Rules & Behavior Summary
- `NpcBuffRequestAction`: Delegates to `BufferNpcPlugIn` first; fallback checks opened NPC buffs matching `MinimumLevel <= player.Level <= MaximumLevel`, applies permanent-configured duration magic effects, and sends too-low / too-strong localized messages when no buff qualifies.
- `PlayerQuestExtensions`: Looks up active `CharacterQuestState` by `(group, number)` matching `Number` or `StartingNumber`, resolves NPC quests filtered by class and `EvolvesIntoInactiveClass`, and filters `GetAvailableQuestsOfOpenedNpc` by level and class.
- `QuestSelectAction` / `QuestStartAction`: Validate quest presence, ensure no active quest in the same `group` (else sends `ShowQuestProgressAsync`), verify `!Equals(LastFinishedQuest, quest) || Repeatable`, enforce `[MinimumCharacterLevel, MaximumCharacterLevel]`, charge `RequiredStartMoney` on start, clear old requirement state, and activate the quest.
- `QuestCompletionAction`: Verifies opened NPC owns the quest (or matches `QuestGiver`), checks inventory item counts/levels, kill counts, client action flag, and class-evolution active flag; destroys required items; grants `Attribute` (with retroactive `(playerLevel - quest.MinimumCharacterLevel) * reward.Value` compensation for `Stats.PointsPerLevelUp`), `Item` (inventory or ground drop fallback), `LevelUpPoints`, `CharacterEvolutionFirstToSecond`, `CharacterEvolutionSecondToThird` (sends master stats), `Experience`, `Money`, `Skill`, and skips `GensAttribution`.
- `QuestCancelAction` / `QuestClientAction` / `QuestExtensions`: Handle quest cancellation and client tutorial completion flags cleanly.

## Discrepancies & Gaps Found
1. **Unimplemented reward — `QuestRewardType.GensAttribution` (`QuestCompletionAction.cs:184-186`)**: Marked `// not yet implemented.`, so quests configured with Gens contribution rewards grant no Gens points.
2. **Missing prerequisite-quest enforcement in `QuestSelectAction` / `QuestStartAction`**: Neither action validates whether a prerequisite quest in the chain was completed (and `CharacterQuestState` only retains a single `LastFinishedQuest` reference per group rather than per-step completion history, overwriting earlier completion state as later quests in the same group finish).
3. **Class-quest `Repeatable` flag mismatch risk (`PlayerQuestExtensions.cs` / `VersionSeasonSix/Quests.cs:84`)**: S6/S9 class progression quests (Indices `0..6`) are one-time per step in the legacy suite; any quest configured with `Repeatable = true` under `CreateQuest` bypasses the `LastFinishedQuest` duplicate guard in `QuestStartAction.cs:52` unless blocked by level range or `QualifiedCharacter`.
4. **Full inventory fallback warning (`QuestCompletionAction.cs:136-147`)**: Drops reward item on the map if inventory is full, which matches safe fallback behavior, though no localized error is shown if `CurrentMap` is null (just logs a warning).
