# Audit Findings — `server/src/GameLogic/PlugIns/SeasonPlan`

- **Unit**: `server/src/GameLogic/PlugIns/SeasonPlan` (7 files, 789 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `SeasonPlanConfiguration.cs`, `SeasonPlanEngine.cs`, `SeasonPlanPlugIn.cs`, `SeasonPlanStateWriter.cs`
  - `SeasonPlanStore.cs`, `SeasonPlanStoreData.cs`, `SeasonStageDefinition.cs`

## Rules & Behavior
- Progressive release timeline (`IDisabledByDefault`, `F8`): gates `ItemPlus10To15`, `MasterLevels`, `Harmony`, `Wing2`, `Fenrir`, `Items380`, `Wing3` across 7 default stages triggered by UTC timestamp, boss kill counts, character count reaching a level (default stage 2: 10 players at Lv 400), or manual admin unlock. Stages 3–7 have no automatic triggers in default template and rely on admin/custom config.

## Real Bugs Found
1. **Integration Bug — `SeasonPlanStoreData.cs` & `CombiningLeftovers` (`CombiningLeftoversStore.cs:142` / `CombiningLeftoversPlugIn.cs:117`)**:
   - `CombiningLeftoversPlugIn` passes `seasonCapOverride: null` to `PlanCombine` and falls back to `TryReadSeasonStageMaxBoxLevel()`, which expects a `MaxBoxLevel` property inside the persisted `"seasonPlan"` JSON section.
   - `SeasonPlanStoreData` never stores `MaxBoxLevel` (only `UnlockedStages`, `UnlockReasons`, `BossKills`, `CharacterLevels`), so `SeasonPlan`'s `MaxBoxLevel` stage cap (`2/3/4/5`) is never enforced in `CombiningLeftovers`.

## Re-check 2026-11-27 (changes 8b9574d1..a108e846)

Commits read (every changed file read in full):

- 478f9cf3 fix(plugins): fix stack deposits/consumption, receipt rollback, buff id collision, and partypass

Evidence:

- SeasonPlanPlugIn.Get prefers the instance with a configuration (two registrations of the plug-in returned the unconfigured one) - fixed.
