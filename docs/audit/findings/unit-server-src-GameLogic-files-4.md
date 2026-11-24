# Unit Audit: `server/src/GameLogic (files 4)`

- **Checked at**: `8b3240ce` (2026-11-24)
- **Status**: `Checked`
- **Reference saved**: [skill-delays-and-player-lifecycle.md](../references/skill-delays-and-player-lifecycle.md)
- **Files read in full (21)**:
  - `server/src/GameLogic/Player.cs`
  - `server/src/GameLogic/PlayerAppearanceData.cs`
  - `server/src/GameLogic/PlayerExperience.cs`
  - `server/src/GameLogic/PlayerInvisibilityExtensions.cs`
  - `server/src/GameLogic/PlayerItemExtensions.cs`
  - `server/src/GameLogic/PlayerMapTransitions.cs`
  - `server/src/GameLogic/PlayerMessageExtensions.cs`
  - `server/src/GameLogic/PlayerMoneyExtensions.cs`
  - `server/src/GameLogic/PlayerMovement.cs`
  - `server/src/GameLogic/PlayerPersistence.cs`
  - `server/src/GameLogic/PlayerState.cs`
  - `server/src/GameLogic/PlayerStorages.cs`
  - `server/src/GameLogic/PlayerSummon.cs`
  - `server/src/GameLogic/PointExtensions.cs`
  - `server/src/GameLogic/PoisonMagicEffect.cs`
  - `server/src/GameLogic/Rand.cs`
  - `server/src/GameLogic/RuntimeCategoryLogger.cs`
  - `server/src/GameLogic/SelfDefenseExtensions.cs`
  - `server/src/GameLogic/ShopStorage.cs`
  - `server/src/GameLogic/ShortExtensions.cs`
  - `server/src/GameLogic/SkillDelays.cs`

## Comparison & Findings
1. **Skill cooldowns (`SkillDelays.cs`) vs S6 & S9 suites**:
   - Checked all 51 skills with non-zero `Delay` in both S6 and S9 skill lists (`the suite's skill list file`).
   - All 48 skills in `SkillDelays.cs` match S6/S9 millisecond values 1:1.
   - The remaining 3 (`262` Chain Drive, `558` Chain Drive Strengthener, `562` Chain Drive Mastery, all `700 ms`) are intentionally excluded with a clear comment because the client emits two hits per cast.
   - `ToleranceFactor = 0.8` permits network jitter without allowing macro bypasses.
2. **Solo vs Party Master-EXP / Catch-up Gate discrepancy (`PlayerExperience.cs` vs `Party.cs`)**:
   - `PlayerExperience.TryGetExperienceKind` (`PlayerExperience.cs:176-192`) checks both `CatchupCapPlugIn.GetEffectiveMaximumLevel` and `SeasonPlanPlugIn.IsFeatureUnlocked(gameContext, SeasonFeatureGate.MasterLevels)` before awarding Master EXP.
   - In `Party.AwardExperienceAsync` (`Party.cs:386-400`), `isAtMaxLevel` checks `Stats.Level == MaximumLevel` directly and calls `player.AddMasterExperienceAsync` without re-checking `SeasonPlanPlugIn.IsFeatureUnlocked(SeasonFeatureGate.MasterLevels)` or `CatchupCapPlugIn.GetEffectiveMaximumLevel`. Recording here so both sides stay aligned if Master Levels are locked on a timed season plan or capped below 400.
3. **Player combat, durability, safezone & spawn guards (`Player.cs`, `PlayerMapTransitions.cs`, `PlayerMovement.cs`, `PoisonMagicEffect.cs`)**:
   - Reflect damage does not reflect poison or already-reflected damage; late damage ticks (`Poison`, `Bleeding`, `Reflect`) are blocked in safezones (unless self-inflicted like Arena shrinking zone).
   - `RecoverFromBlockedSpawnAsync` prevents infinite stack-overflow recursion for offline bots while re-entering `ClientReadyAfterMapChangeAsync`.
   - `PlayerMovement` validates blocked terrain steps, lag-bounded stop offsets, uncharged micro-correction budget (`MaxUnchargedCorrectionDistance = 3.0`), and speed formula (`WalkMovementSpeed = 12.0`, `40 ms` frame reference, `100` terrain scale).
   - `PlayerPersistence` serializes handler mutations against periodic/disconnect saves and syncs shared coins via `SeasonSharedAccounts.SyncCoinsAsync`.
4. **RNG off-by-one note (`Rand.cs`) — informational**:
   - `NextRandomBool(int percent)` (`a = Next(0, 100); return a <= percent;`) gives `percent + 1` outcomes for `1..99` (e.g. `1` -> `2%`, `50` -> `51%`). Worth keeping in mind for drop/mix audits.
