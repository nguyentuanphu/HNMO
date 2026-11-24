# Audit Findings: `server/src/GameLogic/Resets`

**Status**: Checked (all 16 files read in full)
**References**: Custom server feature (HNMO reset / stat-reset / relife system; S6 `408` Gatekeeper NPC)

## Scope & Summary
Audited all 16 files (1,123 lines) covering `/reset`, `/resetinfo`, `/autoreset` (`/uythac`), `/relife`, `/resetstats`, Leo the Helper NPC (`371`), Barracks Gatekeeper NPC (`408`), and base `ResetFeaturePlugIn` / `StatResetFeaturePlugIn` (both `IDisabledByDefault`).

## Rule & Quality Findings

1. **`MoveHomeAsync` incompatible with `LogOut = false` (`ResetCharacterAction.cs:95-107, 206-217`)**:
   - When `configuration.MoveHome == true` and `configuration.LogOut == false`, `MoveHomeAsync` writes directly to `SelectedCharacter.CurrentMap/PositionX/PositionY` instead of calling `WarpToAsync(spawnGate)` (contrast `ResetStatsAction.cs:163`, which uses `WarpToAsync`).
   - Result: if a server sets `LogOut = false` and `MoveHome = true`, the character remains in its old live `GameMap` until relog.
   - Also in `ResetCharacterAction.cs:212-213`, `Rand.NextInt(spawnGate.X1, spawnGate.X2)` is exclusive of `X2`/`Y2` (`NextInt` min..max-1 when not `+ 1`, safe only if `X1 < X2`, would return `X1` or fail if equal depending on `IRandomizer` bounds).

2. **Signed `int` overflow risk in `ResetProgressionCalculator.cs:22-26, 42-48`**:
   - Unlike `GetTotalPointsAfterReset` (line 73), which accumulates in `long` and clamps to `int.MaxValue`, `requiredZen *= nextResetCount` (line 25) and `pointsPerReset *= nextResetCount` (line 45) multiply in `int`, allowing overflow wraparound on large reset counts.

3. **Unchecked window state when talking to NPC (`ResetCharacterAction.cs:53`)**:
   - `PlayerState.EnteredWorld` is required for `/reset` (`_npc == null`), whereas NPC reset intentionally bypasses that guard because talking to Leo the Helper places the player in `NpcDialogOpened`.

4. **Delegation to `ResetOptionsPlugIn` (`ResetCharacterAction.cs:39-44`, `ResetChatCommandPlugIn.cs:33`, `RelifeChatCommandPlugIn.cs:32`)**:
   - When `ResetOptionsPlugIn` is active, `/reset`, `/relife`, `/autoreset`, `/uythac`, and Leo the Helper (`371`) delegate entirely to `ResetOptionsPlugIn` tiers, daily limits, VIP advantages, and delegated offline-leveling reset. Those rules live in `server/src/GameLogic/PlugIns/ResetOptions`.
