# Audit Findings: `server/src/Interfaces` & `server/src/PlugIns`

Checked on: `2026-11-24`

## 1. `server/src/Interfaces`
- **Guild role limits (`GuildRoleLimits.cs`)**:
  - `LevelsPerBattleMaster = 200`, `BaseBattleMasterCount = 1`, `MaxBattleMasterCount(masterTotalLevel) = masterTotalLevel / 200 + 1`.
  - `GuildPosition` enum and `GuildPositionComparer` define guild hierarchy ordering (`GuildMaster`, `BattleMaster`, `Assistant`, `Normal`, `Undefined`).
- **Season instance routing (`HnmoSeasonInstance.cs`)**:
  - Reads `HNMO_SEASON` (`s6`, `s9`, `arena`).
  - Port offsets: `SeasonNinePortOffset = 100`, `ArenaPortOffset = 200`.
  - Database suffixing (`_s9`, `_arena`) and isolated per-season save folders.
- **Host settings & validation (`HostSettings.cs`, `HostSettingsStore.cs`, `HostSettingsValidator.cs`)**:
  - `IsInsideInstallFolder` prevents path traversal (rejects rooted paths, colons, and more than one `..` segment).
  - `ServerProgram` rejects non-single-filename paths.
- **Control operations, crash watch, & logging (`ControlOperations.cs`, `CrashWatchPolicy.cs`, `CrashWatchStore.cs`, `RollingLogWriter.cs`)**:
  - Atomic temp-file + move writes; zip-slip protection in `RestoreBackupZip`; Sydney timezone retention/rotation.
  - Crash watch policy enforces restart hourly cap (`1..60`) and exponential backoff.
- **Inter-server & event contracts (`IGameServer.cs`, `IGuildServer.cs`, `IFriendServer.cs`, `ILoginServer.cs`, `IConnectServer.cs`, `IChatServer.cs`, `*Event.cs`)**:
  - `ILoginServer.TryReserveAccountAsync` reserves offline accounts with a 5-minute lease and positive ownership token (`> 255`) for safe admin writes.
  - `SpecialServerId`: `Offline = 0xFF`, `Invisible = 0xFE`.
  - `MessageType`: `GoldenCenter = 0`, `BlueNormal = 1`, `GuildNotice = 2`, `SlideNotice = 3`.
- **Verdict**: No silent rule regressions found.

## 2. `server/src/PlugIns`
- **Plugin discovery & lifecycle (`PlugInManager.cs`, `PlugInConfiguration.cs`, `PlugInConfigurationChangeApplier.cs`)**:
  - Supports compile-time generated `IPlugInRegistry` (or reflection fallback when `HNMO.PlugIns.DisableGeneratedRegistries` is set), external assemblies under `plugins/`, and runtime Roslyn compilation of `CustomPlugInSource`.
  - `PlugInConfigurationChangeApplier` dynamically activates, deactivates, or reconfigures plugins when configuration changes arrive.
- **Strategy & proxy containers (`StrategyPlugInProvider.cs`, `PlugInContainerBase.cs`, `PlugInProxyTypeGenerator.cs`)**:
  - `StrategyPlugInProvider<TKey, TPlugIn>` maps unique keys to active strategy plugins, logging a warning if a duplicate key is activated so the first active plugin remains effective.
  - `PlugInProxyTypeGenerator` compiles Roslyn proxy implementations of `[PlugInPoint]` interfaces at runtime using `Nito.AsyncEx.AsyncReaderWriterLock` for thread-safe iteration over active plugins.
- **Verdict**: Pure plugin infrastructure; no game rule regressions.
