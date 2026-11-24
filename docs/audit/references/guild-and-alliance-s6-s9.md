# Reference: Guild, Alliance & Battle Soccer Rules (S6 & S9 Suites vs HNMO)

> Sanitized reference captured during the `server/src/GameLogic/PlayerActions/Guild` audit (2026-11-24) so future checks do not need to re-read these suite files.

## 1. Guild & Alliance Settings (`the suite's common settings file` & `CommonServer.cfg` — S6 & S9)
Both Season 6 and Season 9 legacy suite configurations share identical guild settings:
- `GuildCreate = 1`, `GuildDestroy = 1`
- `GuildCreateLevel = 150`
- `MaxGuildMember = 80` (with Dark Lord Guild Master leadership scaling in classic mechanics)
- `CastleOwnerGuildDestroyLimit = 1` (cannot disband guild while owning the castle or during Castle Siege)
- `AllianceMinGuildMember = 10` (minimum members required per guild to form/join an alliance)
- `AllianceMaxGuilds = 3` (maximum guilds in one alliance; note HNMO checks `Stats.MaximumAllianceSize`)
- `EnableBattleSoccer = 1`
- `ServerGroupGuildChatting = 1`, `ServerGroupUnionChatting = 1`
- Chat RGB colors (S9 `the suite's common settings file`): Guild `(0xFF, 0xCC, 0x99)`, Alliance `(0xC8, 0xC8, 0x00)`.

## 2. HNMO `PlayerActions/Guild` Implemented Thresholds
- **Join Guild**: Requester must be Level `>= 6` (`MinimumLevel6`), not already in a guild, and allowed by `IGuildJoinRequestingPlugIn`.
- **Create Guild**: Uses `GuildLimitsPlugIn.GetMinimumCreateLevel` (HNMO `GameplayRules`), filters via `BannedWordsPlugIn.IsGuildNameAllowedAsync`, and calls `IGuildCreatingPlugIn`.
- **Kick / Disband**: Verifies `Account.SecurityCode` (or `Account.PasswordHash` fallback when `SecurityCode` is empty); blocks Guild Master self-kick (disband) when `OwnsTheCastleAsync` is true (`SiegeData.IsOccupied && PersistentGuildId == OwnerGuildId`).
- **Role Assignment**: Assigns `NormalMember`, `BattleMaster`, or `AssistantMaster` (max `1` Assistant Master; Battle Master cap via `GuildRoleLimits.MaxBattleMasterCount(masterTotalLevel)`). No leadership transfer.
- **Guild War & Battle Soccer**: Target score `20` (Normal War) or `100` (Battle Soccer); winning guild receives `+1` score on `GuildServer`.
