# Audit Unit: `server/src/GameLogic/PlugIns/TopRaces`

- **Status**: Checked (on commit `ea5877d5`, 2026-11-24)
- **Files**: 19 files read in full (`TopRaceDefinition.cs`, `TopRaceEngine.cs`, `TopRaceEntry.cs`, `TopRaceLine.cs`, `TopRaceOwedReward.cs`, `TopRaceParticipant.cs`, `TopRacePrize.cs`, `TopRaceResult.cs`, `TopRaceRun.cs`, `TopRacesConfiguration.cs`, `TopRaceSeen.cs`, `TopRacesPlugIn.cs`, `TopRaceStanding.cs`, `TopRaceStateEvent.cs`, `TopRaceStateWriter.cs`, `TopRaceStore.cs`, `TopRaceStoreData.cs`, `TopRaceTable.cs`, `TopRaceWindow.cs`).
- **Rule Category**: Custom HNMO feature — Scheduled leaderboards (resets, levels, boss hunt, PK, events, guild points, monsters, online time).

## Findings

1. **Scoring, tie-breaking & payout**: `TopRaceEngine` computes DST-safe Daily/Weekly/Monthly/Custom windows, breaks ties by earliest `UpdatedAtUtc`, and tracks `PaidReferences` per prize/guild member. `TopRacesPlugIn` excludes same-account, duel, and mini-game PK kills and dispatches announcements/payouts from the lowest server ID.
2. **Notes**:
   - `TopRacesPlugIn.PayCharacterAsync` (`lines 583-600`) calls `engine.NotePaid` without checking the boolean return value of `MissionCoinGranter.GrantAsync`.
   - `TopRaceStore.Changed()` sets `_changed = true` without calling `MarkDirty`/`MarkFullScanPending` on `_runsSync`/`_seenSync`/`_owedSync` while `SaveIfChanged()` passes `useDirtySetOnly: true`.
