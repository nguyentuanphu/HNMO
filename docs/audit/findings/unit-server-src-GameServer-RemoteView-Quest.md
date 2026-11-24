# Unit: server/src/GameServer/RemoteView/Quest

Checked against: Season 6/9 client legacy quest class bitfield protocol.

Scope: `server/src/GameServer/RemoteView/Quest` (`17` files, `1179` lines).

## Findings

| File & Line | Rule / Constant | Suite Role & Value | Result |
|---|---|---|---|
| `CharacterClassExtensions.cs:20-28` | `GetBaseClass` traverses `NextGenerationClass` chain recursively so evolved classes report base class in quest packets | Standard MU quest wire protocol compatibility | Same |
