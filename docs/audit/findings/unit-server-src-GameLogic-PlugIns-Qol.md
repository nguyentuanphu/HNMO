# Audit Unit: `server/src/GameLogic/PlugIns/Qol`

- **Status**: Checked (on commit `512795cc`, 2026-11-24)
- **Files**: 8 files read in full (`MineChatCommandPlugIn.cs`, `MiningRewardEntry.cs`, `QolConfiguration.cs`, `QolPlugIn.cs`, `QolState.cs`, `QolStore.cs`, `RequestsChatCommandPlugIn.cs`, `SrvInfoChatCommandPlugIn.cs`).
- **Rule Category**: Custom HNMO feature — `/requests`, `/srvinfo`, and `/mine`.

## Findings

1. **Defect (partial reward on full Zen in `/mine`)**: In `QolPlugIn.HandleMineCommandAsync` (`lines 248-276`), if a `MiningRewardEntry` configures both `ItemGroup >= 0` and `Zen > 0` and `player.TryAddMoney(reward.Zen)` returns `false`, the item is already added to inventory while the method returns `false` before updating `_lastMineUtc`, allowing repeated item grants with no cooldown. (Default rewards set either item or Zen, not both.)
2. **Thread-safe store & cleanup**: `QolStore` synchronizes on `_stateStore.SyncRoot` and caps `RecentMineLogs` at `50`. Player dictionaries (`_requestsBlocked`, `_lastMineUtc`) are cleaned up on `IsDisconnectedOrFinished()`.
