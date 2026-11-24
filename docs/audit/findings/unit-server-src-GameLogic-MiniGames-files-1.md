# Audit Findings: `server/src/GameLogic/MiniGames (files 1)`

- **Unit**: `server/src/GameLogic/MiniGames (files 1)` (37 files, 5,482 lines)
- **Status**: `Checked` on 2026-11-24
- **Reference saved**: [minigames-bc-cc-ds-it-s6-s9.md](file:///D:/WorkSpace/HNMO-ag/docs/audit/references/minigames-bc-cc-ds-it-s6-s9.md)

## Files Reviewed (in full — all 37 files)
- Contexts & Core: `BloodCastleContext.cs` (1–392), `ChaosCastleContext.cs` (1–707), `ChaosCastleDropGenerator.cs` (1–102), `ChaosCastleStatus.cs` (1–41), `DevilSquareContext.cs` (1–138), `IllusionTempleContext.cs` (1–1157), `IllusionTempleScore.cs` (1–86), `IllusionTempleTeam.cs` (1–21), `MiniGameContext.cs` (1–948), `MiniGameManager.cs` (1–180), `MiniGamePlayerRegistry.cs` (1–175), `MiniGameRewardService.cs` (1–300), `MiniGameChangeEventProcessor.cs` (1–240), `MiniGameSpawnWaveRunner.cs` (1–147), `ChangeEventContext.cs` (1–63), `BloodCastleItemExtensions.cs` (1–52), `MiniGameMapKey.cs` (1–46), `MiniGameState.cs` (1–41), `SkippableDelay.cs` (1–126), `IMiniGameManager.cs` (1–66).
- View Interfaces (17 files): `IBloodCastleScoreTableViewPlugin.cs`, `IBloodCastleStateViewPlugin.cs`, `IChangeTerrainAttributesViewPlugin.cs`, `IChaosCastleScoreTableViewPlugin.cs`, `IChaosCastleStateViewPlugin.cs`, `IIllusionTempleEventStateViewPlugIn.cs`, `IIllusionTempleHolyItemRelicsViewPlugIn.cs`, `IIllusionTempleScoreTableViewPlugIn.cs`, `IIllusionTempleSkillEndedViewPlugin.cs`, `IIllusionTempleSkillPointUpdateViewPlugin.cs`, `IIllusionTempleSkillUsageResultViewPlugin.cs`, `IIllusionTempleStateViewPlugin.cs`, `IMiniGameScoreTableViewPlugin.cs`, `IShowIllusionTempleUserCountViewPlugIn.cs`, `IShowMiniGameEnterResultPlugin.cs`, `IShowMiniGameOpeningStatePlugIn.cs`, `IUpdateMiniGameStateViewPlugIn.cs`.

## Rules & Behavior Summary
- `MiniGameContext` / `MiniGamePlayerRegistry` / `MiniGameManager`: Atomic state transitions (`Open -> Closed -> Playing -> Ended -> Disposed`) synced with entering, per-player/per-party/shared instance keys, disconnect position tracking and `TryRejoinAsync`, equipment validation, `MinimumPlayerCount` entrance fee refund on cancellation, terrain change history replay on rejoin, wave spawning, and `SkippableDelay`.
- `MiniGameRewardService`: Exclusive reward evaluation by rank, win/party/alive/dead/kill flags, partial money fit capped at `MaximumInventoryMoney`, inventory-or-drop item rewards, and `MiniGameRankingEntry` persistence.
- `BloodCastleContext` / `ChaosCastleContext` / `DevilSquareContext` / `IllusionTempleContext`: Event-specific scoring, statue/gate/relic flows, CC island shrinking & explosion knockback, IT random shuffled balanced teams with temporary parties & special skills (`210..213`).

## Discrepancies & Observations
1. **Illusion Temple PvP kill SP (`IllusionTempleContext.cs:89`)**: `SkillPointsPerPlayerKill = 1` vs `OpponentKill = 5` in S6 `IGC_IllusionTemple_Classic.xml`.
2. **Dark Horse not excluded in `ChaosCastleContext.IsItemAllowedToEquip` (`ChaosCastleContext.cs:111-118`)**: Blocks `Uniria`, `Dinorant`, `Fenrir`, and transformation rings, but does not exclude Dark Horse (`13:4`). Also intentionally omits explosion damage on knockback (noted in class remarks).
3. **`BloodCastleContext.cs:232` score table EXP display**: Reports only `MiniGameRewardType.Experience` (fixed bonus) in `ShowScoreTableAsync`, excluding `ExperiencePerRemainingSeconds` even though `MiniGameRewardService` awards both.
4. **Hardcoded CC jewel drop table (`ChaosCastleDropGenerator.cs:18-28`)**: Bless/Soul drop counts per CC level (`0/2, 1/1, 1/2, 1/2, 2/1, 2/2, 2/3`) are static rather than data-driven (`TODO`).
5. **Unchecked `First` in `BloodCastleContext.cs:250` & `ChaosCastleContext.cs:252`**: Both index into `_highScoreTable` with `.First(t => t.Name == player.Name)`; assumes every finisher still present in `ShowScoreAsync` had an entry in `_gameStates`.
6. **S6 Illusion Temple extras absent**: `MinUseSkillCount = 3` reward gate and `GoldenFenrirChangeUp` probabilities (`10%/20%/30%`) are not modeled in `IllusionTempleContext`.
