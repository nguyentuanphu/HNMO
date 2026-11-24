# Unit: server/src/Interfaces

Checked against: classic MU / Season 6 & Season 9 guild hierarchy & Battle Master limit role.

Scope: `server/src/Interfaces` (`86` files, `5667` lines).

## Summary

- Most files define cross-assembly interfaces, admin/host configuration (`HostSettings`, `CrashWatchPolicy`, `MaintenanceSettings`, `RollingLogWriter`), and localization helpers (`LocalizedString`).
- Two files define game-rule constants/hierarchy: `GuildRoleLimits.cs:16-31` and `GuildPositionComparer.cs:63-73`.

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `GuildRoleLimits.cs:16-31` | `LevelsPerBattleMaster = 200`, `BaseBattleMasterCount = 1`, `MaxBattleMasterCount = (masterTotalLevel / 200) + 1` (combined normal + master level); max `1` Assistant Master | Classic MU Guild Battle Master cap (`1 + floor(GM_level / 200)`, max `1` Assistant Master; classic caps normal Lv <= 400 while HNMO uses combined normal + master level) | Same formula / uses combined level |
| `GuildPositionComparer.cs:63-73` | Guild hierarchy rank order: `GuildMaster (0) > AssistantMaster (1) > BattleMaster (2) > NormalMember (3)` | Classic MU guild rank hierarchy | Same |
| `IGameServer.cs`, `IGuildServer.cs:1-60`, `HostSettings.cs`, `CrashWatchPolicy.cs`, `LocalizedString.cs`, etc. | Service contracts, host operations, logging, and localization | Service / infrastructure contracts | No game rules |
