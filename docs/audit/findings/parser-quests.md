# Parser Findings: Quests (`diff-quests.txt`)

- **Checked on**: 2026-11-24
- **Outputs**: [diff-quests.txt](../data/diff-quests.txt) (474 lines), [s9/diff-quests.txt](../data/s9/diff-quests.txt) (474 lines)
- **Sources**: both suites' class quest XML (`ClassQuest.xml`), Season 6 custom quest INI (`QuestSystem.ini`), both suites' episode Lua scripts (`Scripts/Quests/Quest_Ep*.lua`, `Quest_Reward_Ep*.lua`), and HNMO `server/src/Persistence/Initialization/VersionSeasonSix/Quests.cs`

## Fix Summary

`parseSuiteQuests` previously only checked whether `QuestInfo.Type` or `SubType` differed between Season 6 and Season 9 (producing an empty `QuestInfo differences` section and no HNMO comparison). The updated parser extracts `<QuestCondition>` (`MinLevel`, `ReqMoney`) and `<Job>` (`ReqItemCat/Index`, `MonsterMin/MaxLevel`, `DropRate`, `RewardType/Count`), `QuestSystem.ini` custom rows, and 456 Episode Lua quest definitions (`SetQuestMonsterKill`, `SetQuestGetItem`, `SetQuestRewardExp/Zen/Contribute/Item`), comparing them directly against HNMO `Quests.cs`.

## Hand-Verified Sample Values

1. **Class Quest `4` (Certificate of Strength / Evidence of Strength, NPC `406`)**:
   - Hand check in both suites' `ClassQuest.xml`: `MinLevel="380"`, `ReqMoney="5000000"`, `<Job>` drops `14:65` (Mob 63), `14:66` (Mob 309), `14:67` (Mob 77) each with `DropRate="1000"` (`10%`) and final `RewardCount="10"` stat points.
   - HNMO `Quests.cs:175-197`: `MinimumCharacterLevel = 380`, `RequiredStartMoney = 5_000_000` (**same**); drop chances `3% / 4% / 5%` (`3.0 / 100.0`, `4.0 / 100.0`, `5.0 / 100.0`) and `pointReward.Value = 20` (**differs** — suites give `10` pts in Quest 4 + `20` pts in Quest 5 = `30`, whereas HNMO swaps them to `20` + `10` = `30`).
2. **Episode `18:1` (`Spider Hunt`, NPC `257`)**:
   - Hand check in both suites' `Scripts/Quests/Quest_Reward_Ep18.lua`: `SetQuestMonsterKill(ASK1, Ep18, 1, 3, 10)` + `SetQuestRewardExp(Ep18, 1, 2000)`.
   - HNMO `Quests.cs:360-362`: `CreateQuest(..., 18, 0, 1, 2, 1, 14, 257).WithMonsterKillRequirement(10, 3, ...).WithExperienceReward(2000, ...)` (**same**).
3. **Class Quest `1` (`Four Treasures of Mu`) note**: Suite XML guards entry with prerequisite `Index="0"` (`MinLevel="0"`, Zen `2M`) while HNMO `Quests.cs:277` sets `MinimumCharacterLevel = 150`.
4. **Episode totals**: `456` suite Lua episode steps vs `247` `CreateQuest` blocks in `Quests.cs` -> `same = 101`, `differs = 146`, `missing_in_HNMO = 209`.
