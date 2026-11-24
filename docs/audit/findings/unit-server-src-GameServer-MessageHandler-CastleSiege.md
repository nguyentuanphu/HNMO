# Unit: server/src/GameServer/MessageHandler/CastleSiege

Checked against: Season 6 Castle Siege NPC #223 (Senior) handler validation.

Scope: `server/src/GameServer/MessageHandler/CastleSiege` (`28` files, `1232` lines).

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `CastleSiegeDefenseBuyHandlerPlugIn.cs:22`, `Repair:22`, `Upgrade:23`, `HuntZoneToggle:22`, `CastleSiegeNpcListHandlerBase.cs:15` | Validates player opened dialog with `SeniorNumber = 223` before forwarding Castle Siege management operations | Season 6 Castle Siege Senior NPC (#223) guard | Same |
