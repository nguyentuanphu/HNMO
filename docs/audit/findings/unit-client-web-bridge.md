# Unit: client/web/bridge

Checked against: ASP.NET web bridge rate-limit & security rule scan (`18` unit files, `5889` lines).

Scope: `client/web/bridge`.

## Findings

| File & Line | Rule / Constant | Role & Value | Result |
|---|---|---|---|
| `AccountGuessGuard.cs:11-45` | `MaximumFailures = 10` within `15m` window triggers `15m` lock per account login | Anti-bruteforce guard (HNMO web security) | HNMO custom |
| `VisitorLimits.cs:9-40` | `ConnectionsPerAddress = 6`, `NewConnectionsPerMinute = 30`, `PacketsPerSecond = 60`, `PacketBurst = 200` (loopback exempt) | WebSocket DoS protection (HNMO ops) | HNMO custom |
| `AdminGateway.cs:12-21`, `ControlEndpoints.cs`, `Portal.cs`, `GameFiles.cs` | Static hosting, admin reverse proxy, data pack & maintenance orchestration | Bridge infrastructure | No game rules |
