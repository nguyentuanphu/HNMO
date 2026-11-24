# Audit Findings — `server/src/GameLogic/PlugIns/Moderation`

- **Unit**: `server/src/GameLogic/PlugIns/Moderation` (2 files, 583 lines, checked on `2026-11-24`)
- **Files reviewed**:
  - `BanManagerService.cs`, `BanRecord.cs`

## Behavior Summary
- Account/character bans & chat mutes + salted HMAC-SHA256 `NetworkBan` / `DeviceBan` records backed by `IHnmoPlayerStateRowBackend`.
- Fail-closed if salted rows exist without `moderation.salt`; Sydney local time expiry formatting (`Australia/Sydney` / `AUS Eastern Standard Time`); IPv4-mapped IPv6 normalization.

## Observations
1. **CIDR Range Non-Match — `BanManagerService.cs:108, 409-430`**:
   - `Issue` documents `"raw IP/CIDR string"` for `NetworkBan`, but `NormalizeIdentity` / `HashIdentity` hashes the raw string verbatim via HMAC-SHA256. A CIDR entry like `1.2.3.0/24` will never match an individual client IP `1.2.3.4` in `FindActiveLoginBlock`.
