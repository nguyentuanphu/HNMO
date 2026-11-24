# Audit Unit: `server/src/GameLogic/PlugIns/Community`

- **Status**: Checked (on commit `15c801d2`, 2026-11-24)
- **Files**: 16 files read in full (`CommunityAccountState.cs`, `CommunityCoinReward.cs`, `CommunityConfiguration.cs`, `CommunityDropFilter.cs`, `CommunityEngine.cs`, `CommunityEventDefinition.cs`, `CommunityEventEntry.cs`, `CommunityHighlight.cs`, `CommunityOwedReward.cs`, `CommunityPlayerFacts.cs`, `CommunityPlugIn.cs`, `CommunityPostSetting.cs`, `CommunityStateEvent.cs`, `CommunityStateWriter.cs`, `CommunityStore.cs`, `CommunityStoreData.cs`).
- **Rule Category**: Custom HNMO feature — Discord/Zalo rewards, community event codes/winners, and highlight posts.

## Findings

1. **Deduplication & cooldowns**: `CommunityEngine.ClaimDiscord` enforces once-per-account and once-per-Discord-user (`PaidDiscordUsers`). `EnterCode` enforces once-per-account within `[Start, End)` Sydney wall-clock window. `TryTakeDropSlot` rate-limits rare drop posts per player (`DropCooldownSeconds = 30`).
2. **Notes**:
   - `CommunityPlugIn.PayCoinsAsync` (`lines 560-565`) calls `engine.MarkCoinsPaid(entry)` even when `MissionCoinGranter.GrantAsync` returns `false`.
   - `CommunityStore.Changed()` sets `_changed = true` without calling `MarkDirty`/`MarkFullScanPending` on `_accSync`, `_entrySync`, `_owedSync` while `SaveIfChanged()` passes `useDirtySetOnly: true` (verify `HnmoFeatureRowSync` in `Persistence`).
