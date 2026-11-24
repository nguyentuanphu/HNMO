# Unit Audit: `server/src/GameLogic/PlugIns (files 2)`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (32 files read in full)

## Summary of Rules & Behavior

- **Anti-Cheat / Speedhack (`SpeedHackDetectPlugIn`, `SpeedHackDetectConfiguration`, `WalkSpeedCheck`, `WalkSpeedValidator`, `WalkSpeedWindow`, `ViolationPolicy`, `SpeedHackViolationRecorder`)**:
  - Sliding-window walk pace check (`DefaultWindowSeconds = 15`, `NormalStepDelayMs = 300`, `BaseStepDelayMarginMs = 50`, `MinStepDelayMs = 50`, `WalkSpeedToleranceMs = 2000`, `EnforceWalkSpeed = false` by default).
  - Exponential decay violation score (`DefaultRejectThreshold = 6.0`, `DefaultHalfLifeMinutes = 5.0`), debounced warning history (`MaxWarnings = 3`, `AlertDebounceSeconds = 5`, `WarningHistoryHours = 1`), and GM/Spectator exemption (`CanPunish`).
  - Excludes `OfflinePlayer` (`IsServerControlled`) from client-timing checks.
- **Self Defense (`SelfDefensePlugIn`)**:
  - 1-minute default timeout; skips PKs (`>= PlayerKiller1stStage`), duelists, Chaos Castle / mini-games with `AllowPlayerKilling`, rival guilds, Gens exemptions, 0-damage hits, and reflected damage.
- **Item Stacking Transformations**:
  - `SignOfDimensionsStackedPlugIn` (`14:110` -> `14:111` Mirror of Dimensions)
  - `SuspiciousScrapOfPaperStackedPlugIn` (`14:101` -> `14:102` Gaion's Order)
  - `SymbolOfKundunStackedPlugIn` (Symbol of Kundun -> Lost Map)
- **World / Website / Announcements**:
  - `SoccerSpawnGatePlugIn`, `UpdateIsInSafezoneAfterPlayerMoved`, `WarpGateRules`, `WeatherUpdatePlugIn`, `ShowMessageToAllWhenPlayerEnteredWorldPlugIn`, `ServerAnnouncementsPlugIn`, `WebsitePlugIn`, `WebsiteBrand`, `WebsiteConfiguration`.

No game-rule defects found.
