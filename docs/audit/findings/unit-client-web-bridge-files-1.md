# Unit Audit: `client/web/bridge (files 1)`

- **Unit**: `client/web/bridge (files 1)`
- **Checked at**: `8cb8539b` (`2026-11-25`)
- **Files in unit**: `17` files (`5,938` counted `.cs` lines): `AccountGuessGuard.cs`, `AdminGateway.cs`, `ControlEndpoints.cs`, `ControlMaintenanceService.cs`, `DataPack.cs`, `GameFiles.cs`, `GameLauncher.cs`, `HostOnly.cs`, `LauncherFileLogger.cs`, `PortOwner.cs`, `Portal.cs`, `Program.cs`, `RelayPorts.cs`, `SeasonData.cs`, `Updater.cs`, `VisitorLimits.cs`, `Website.cs`
- **Status**: `Checked`

## Rules & Behavior Checked (`file:line`)

| Rule / Behaviour | Location | Client / Bridge Value / Behaviour | Server / Security Reference | Match |
|---|---|---|---|---|
| Website password brute-force lockout | `AccountGuessGuard.cs:11-55` | Locks an account after `MaximumFailures = 10` wrong passwords within `Window = 15m` for `LockTime = 15m` (case-insensitive trimmed `loginName`); clears failures on success once unlocked | Web portal security policy (Audit 2.8) | HNMO custom |
| Per-IP connection & packet rate limits | `VisitorLimits.cs:7-51,94-112` | Loopback exempt (`IPAddress.IsLoopback`); non-loopback limits `ConnectionsPerAddress = 6`, `NewConnectionsPerMinute = 30`, token bucket `PacketsPerSecond = 60` with `PacketBurst = 200` | WebSocket relay DoS protection (`appsettings.json` `"Limits"`) | HNMO custom |
| Relay & proxy packet/body bounds | `Program.cs:222`, `Portal.cs:349`, `Website.cs:128`, `GameFiles.cs:193` | `MaximumPacketSize = 0xFFFF`; `Portal.cs` brand value length `500`; `Website.cs` request body `128 * 1024`; `GameFiles.cs` HTTP range chunking (`65536`) & cache headers | Web bridge HTTP/WS boundary guards | Same |
| Proxy, updater, launcher & static file serving | `AdminGateway.cs`, `ControlEndpoints.cs`, `ControlMaintenanceService.cs`, `DataPack.cs`, `GameFiles.cs`, `GameLauncher.cs`, `HostOnly.cs`, `LauncherFileLogger.cs`, `PortOwner.cs`, `RelayPorts.cs`, `SeasonData.cs`, `Updater.cs` | Local launcher orchestration, data-pack caching, season static asset routing, update staging, and reverse proxy endpoints | Bridge infrastructure (no MU gameplay formulas) | No game rules |
