# Unit Audit: `server/src/GameLogic/PlugIns/Missions`

- **Audit date**: `2026-11-24`
- **Status**: `Checked` (26 files read in full; HNMO-custom daily/weekly missions, streak bonus, online-time coins, level/reset milestones, invites, and login calendar — no Season 6 / Season 9 legacy counterpart)

## Files Audited

- `CoinGrant.cs`
- `DayCoins.cs`
- `InviteCoinsConfiguration.cs`
- `LevelCoins.cs`
- `LoginCalendarConfiguration.cs`
- `MilestoneCoinsConfiguration.cs`
- `MissionAccountState.cs`
- `MissionBoardEvent.cs`
- `MissionBoardWriter.cs`
- `MissionCharacterState.cs`
- `MissionClock.cs`
- `MissionCoinGranter.cs`
- `MissionConfigurationText.cs`
- `MissionDefinition.cs`
- `MissionEngine.cs`
- `MissionItemReward.cs`
- `MissionProgressState.cs`
- `MissionReward.cs`
- `MissionsConfiguration.cs`
- `MissionsPlugIn.cs`
- `MissionStore.cs`
- `MissionStoreData.cs`
- `OnlineTimeCoinsConfiguration.cs`
- `OwedCoinsState.cs`
- `WebsiteMissionActions.cs`
- `WebsiteMissionSnapshot.cs`

## Summary of Rules & Behavior

1. **Daily & Weekly Missions (`MissionEngine`, `MissionsPlugIn`, `MissionClock`)**:
   - Tracks `KillMonsters`, `FinishEvent`, `WinEvent`, `ChaosMachineMix`, `PickUpZen`, `LevelUp`, `PlayMinutes` per character in `MissionClock` day/week boundaries (default `Australia/Sydney`, `ResetHour = 0`).
   - Rewards can grant items, Zen, and cash-shop coins; item/Zen overflow rolls back added items and returns `BagFull`/`ZenFull` while leaving the mission unclaimed.
2. **Daily Streak & Earned Coins (`MissionEngine`)**:
   - Streak advances once per game day when `StreakRequired` daily missions are finished, and awards `StreakBonus` every `StreakBonusDay` (default 7).
   - Earned coin sources (`OnlineTime`, `Milestones`, `Invite`, `LoginCalendar`) share an account `EarnedDailyCap` (default 300); overflow from one-time rewards is queued in `Owed` and paid out on subsequent days via `TakeOwed`.
   - `Invite`: blocks self-invites, already-invited accounts, accounts at/above `RequiredLevel`, and same-IP invites (`IsSameAddress` ignores loopback relays).
3. **Persistence & Website Integration (`MissionStore`, `WebsiteMissionActions`, `WebsiteMissionSnapshot`)**:
   - `MissionStore.Changed()` properly marks `MarkFullScanPending()` on both `HnmoFeatureRowSync<MissionCharacterState>` and `HnmoFeatureRowSync<MissionAccountState>`.
   - `WebsiteMissionSnapshot` deep-clones state before calling `MissionBoardWriter.WriteBoard` so read-only website queries cannot mutate live state or generate persistent invite codes.
   - `WebsiteMissionActions.ClaimAsync` executes inside `player.RunPersistenceExclusiveAsync` and re-verifies character/account ownership under the lock.

## Findings / Observations

1. **`ClaimCalendarAsync` ignores `MissionCoinGranter.GrantAsync` failure (`MissionsPlugIn.cs:185-189`)**: `given = grant.Amount` is set without checking whether `MissionCoinGranter.GrantAsync` returned `true`.
2. **Fire-and-forget `AddProgress` persistence (`MissionsPlugIn.cs:317-337`)**: `CharacterLeveledUp` calls `AddProgress` and dispatches `CheckLevelRewardsAsync` in `Task.Run` (which calls `this.Save()`), whereas other event handlers (`AttackableGotKilledAsync`, `ItemCraftedAsync`, `MoneyPickedUpAsync`) rely on the 1-minute `ExecuteTaskAsync` tick to flush changes to disk.
