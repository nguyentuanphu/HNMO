# ChatGPT Work Audit Report (2026-11-19, Australia/Sydney)

Full audit of ChatGPT's pull requests (`#2, #3, #4, #5, #6, #8, #9, #12, #16, #26, #29, #33, #39, #62, #64, #79–#83`) rebased and integrated cleanly onto `antigravity-integration` (`650c0431`) inside branch `chatgpt-integration` (PR `#26`) on Windows (`win-x64`).

---

## 1. PR Inventory Reviewed

| PR | Branch | Scope | Integration Status |
|---|---|---|---|
| `#2` | `chatgpt-login-hint` | Simplified web login hint & `client/web/src/net/session.ts` error mapping | Merged via `#26`; fixed `session.ts` merge import |
| `#3` | `chatgpt-website-themes` | Five admin-selectable HNMO website themes (`home.html`, AdminPanel `Website` settings) | Merged via `#26` |
| `#4` | `chatgpt-vietnamese` | Expanded Vietnamese coverage across web game & AdminPanel | Merged via `#26`; ResX EN/VI parity verified (0 missing keys) |
| `#5` | `chatgpt-apikeys-help` | API keys guidance on `ApiKeys.razor` | Merged via `#26`; preserved `#api-keys-help-toggle` inside `.page-help` |
| `#6` | `chatgpt-buffs-vi` | Vietnamese names/descriptions for buffs (`client/src/bin/Data/Buffs/Buffs.json`, web data) | Merged via `#26` |
| `#8` | `chatgpt-editor-vi` | C++ editor VI catalogs (`client/src/MuEditor/**`, `.github/workflows/editor-localization.yml`, `client/tools/check-editor-vi.py`) | Merged via `#26` — **Flagged (Scope)**: HNMO is WEB ONLY; C++ desktop editor files are dormant/legacy only |
| `#9` | `chatgpt-phoenix-shot` | Phoenix Shot area attack fix | Merged via `#26` |
| `#12` | `chatgpt-giftcode-salt-migration` / `chatgpt-giftcode-limit` | Network & device gift-code claim limits (`F5 23`) | Merged via `#26` |
| `#16` | `chatgpt-shop-editor` | NPC Shop Editor (`/merchants` workspace, `/merchants/legacy`, `ItemPicker`) | Merged via `#26`; fixed `.page-help` wrapper & `AdminNavCatalog` route for `merchants/legacy` |
| `#26` | `chatgpt-integration` | Combined integration branch | Cleanly rebuilt atop `antigravity-integration` (`650c0431`) |
| `#29` | `chatgpt-account-page` | Account dashboard, confirmed email changes, SMTP settings (`/email-settings`), coin history | Merged via `#26`; fixed `HnmoStateStore` Windows save error, DI in `EveryAdminPageHelpTests`, and `package-hnmo.sh` bundling for `account.html` |
| `#33` | `chatgpt-admin-help` | Reusable `<PageHelp>` & bilingual action hints across AdminPanel | Merged via `#33`; aligned all pages to `.page-help` + `button[title]` |
| `#39` | `chatgpt-vip` | VIP tiers, coins, per-server EXP/Drop/Zen rates, `Stats.IsVip`, VIP servers (`F5 25/26`) | Merged via `#39`; fixed `AdminWriteSettings` guard & `gc.Attributes.Add(vipAttr)` |
| `#62` | `chatgpt-giftcode-salt-migration` | Preserve gift-code identity salt on state migration | Already merged in `#26` |
| `#64` | `chatgpt-gift-box` | Gift Box & Mail (`F5 27/28`, `/gift-box`) | Merged cleanly atop `ServerSettingsOperations`; registered `/gift-box` in `AdminNavCatalog` |
| `#79` | `chatgpt-ban-manager` | Ban & Mute Manager (`F5 29`, `/bans`) | Merged cleanly; fixed critical timed-ban expiry bug, JSON construction, read-only guard, and login reservation |
| `#80` | `chatgpt-character-tools` | Character Tools (`/character-tools`) | Merged cleanly; registered in `AdminNavCatalog` |
| `#81` | `chatgpt-gm-manager` | GM Manager & in-game GM commands (`/gm`) | Merged cleanly; added login reservation before offline `CharacterStatus` changes |
| `#82` | `chatgpt-news-manager` | News Manager (`F5 2A`, `/news`, `#loginNewsPanel`, homepage feed) | Merged cleanly; fixed DST invalid-time parsing, read-only guard, `.page-help`, and button titles |
| `#83` | `chatgpt-admin-redesign` | Task-grouped menu, `Ctrl+K` global search, favourites/recent, setup checklist, `/admin-guide` split | Merged cleanly; bounded account search and completed `AdminNavCatalog` |

---

## 2. Findings & Remediation Status

### Critical

1. **Expired timed account/character bans remained permanently enforced after expiry**
   - **Location**: `server/src/GameLogic/PlayerActions/LoginAction.cs:263-288`, `server/src/GameLogic/PlayerActions/Character/SelectCharacterAction.cs:53-73`, `server/src/Web/AdminPanel/API/WebsiteLoginTicketController.cs:107-135`, `server/src/GameLogic/PlugIns/Moderation/BanManagerService.cs:360-405`
   - **Repro**: Issue a timed `AccountBan` (`expiresAtUtc = T + 1h`) or timed `CharacterBan` while the account is offline. `BanAdminOperations.IssueAsync` persists `account.State = AccountState.TemporarilyBanned` (or `character.CharacterStatus = CharacterStatus.Banned`). After `T + 1h` passes, `FindActiveLoginBlock` and `FindActiveCharacterBan` return `null`, yet `ValidateAccountStateAsync`, `SelectCharacterAction.SelectCharacterAsync`, and `WebsiteLoginTicketController.BuildBlockResponse` still reject the player indefinitely based solely on the stale database status.
   - **Fix Applied**: Added `BanManagerService.HasExpiredAccountBanOnly` and `BanManagerService.HasExpiredCharacterBanOnly` (`BanManagerService.cs:376-403`). When no active ban matches and `HasExpired*BanOnly` confirms the block came from an expired timed ban record, login, web-ticket issuance, and character selection now permit entry (while manual DB bans without expired moderation records remain blocked). Added regression test `BanManagerEnforcementTests.ExpiredTimedBansAllowLoginAndCharacterSelectionAsync`.

### High

2. **Windows compatibility failure in `WebsiteMailSettingsTests.FailedSaveLeavesThePreviousSettingsActive`**
   - **Location**: `server/src/GameLogic/PlugIns/HnmoStateStore.cs:189-215, 396-401`, `server/src/Web/AdminPanel/Services/WebsiteMailSettingsStore.cs`
   - **Repro**: The test creates a directory at `this._filePath` to simulate an unwritable target path. On Linux, `File.Move(..., overwrite: true)` throws `IOException`; on Windows, moving onto an existing directory throws `UnauthorizedAccessException`, and `WriteSection` left the failed value mutated inside `_sections` in memory.
   - **Fix Applied**: `HnmoStateStore.SaveLocked` now throws `IOException` immediately if `Directory.Exists(this._filePath)`, and `HnmoStateStore.WriteSection` restores or removes `_sections[sectionKey]` if `SaveLocked()` throws. Verified `WebsiteMailSettingsTests.FailedSaveLeavesThePreviousSettingsActive` passes on Windows.

3. **Unsafe string `.Replace("{", ...)` JSON mutation in blocked login ticket response**
   - **Location**: `server/src/Web/AdminPanel/API/WebsiteLoginTicketController.cs:129-134`
   - **Repro**: `BanManagerService.BuildNoticeJson(effective).Replace("{", "{\"error\":\"blocked\",", StringComparison.Ordinal)` replaces *every* `{` character across the entire serialized JSON string, corrupting ban notices if `reasonEn`, `reasonVi`, or target fields contain `{`.
   - **Fix Applied**: Replaced with `System.Text.Json.Nodes.JsonNode.Parse(...)!.AsObject()` setting `payload["error"] = "blocked"`.

4. **Missing `try/reserve/release` login reservation around offline DB mutations in Ban & GM Admin Operations**
   - **Location**: `server/src/Web/AdminPanel.Operations/BanAdminOperations.cs:117-245`, `server/src/Web/AdminPanel.Operations/GmManagerOperations.cs:96-205`
   - **Repro**: Both `BanAdminOperations` (`IssueAsync`/`LiftAsync`) and `GmManagerOperations` (`AssignGmAsync`/`RevokeGmAsync`) only read `login.GetSnapshotAsync()` before mutating offline `Account.State`, `Account.ChatBanUntil`, or `Character.CharacterStatus`, leaving a TOCTOU race if a player logs in between the snapshot check and `SaveChangesAsync`.
   - **Fix Applied**: Added `TryMutateOfflineAccountAsync` / `ExecuteWithOfflineReservationAsync` acquiring `login.TryReserveAccountAsync` and releasing in `finally` before offline entity mutation. Updated `BanManagerPageTests` and `GmManagerPageTests` mocks to permit `TryReserveAccountAsync`.

5. **Missing `AdminWriteSettings` read-only enforcement in Moderation, News, and Server Settings write operations**
   - **Location**: `server/src/Web/AdminPanel.Operations/BanAdminOperations.cs:35-89, 178-184`, `server/src/Web/AdminPanel.Operations/NewsAdminOperations.cs:24-145`, `server/src/Web/AdminPanel.Operations/ServerSettingsOperations.cs:25-80`
   - **Repro**: When AdminPanel is configured with `AdminWriteSettings.ReadOnly = true`, `BanAdminOperations.IssueAsync`/`LiftAsync`, `NewsAdminOperations.SavePostAsync`/`BroadcastPostAsync`/`DeletePostAsync`, and `ServerSettingsOperations.SaveAsync` did not call `AdminChangeExecution.RequireAsync`.
   - **Fix Applied**: Added optional `AdminWriteSettings? writeSettings = null` parameter (preserving backwards-compatible test instantiation and receiving DI `AdminWriteSettings` in production) and enforced `AdminChangeExecution.RequireAsync` on all write/broadcast entry points.

6. **Packaged Windows script bundling omission for `account.html`**
   - **Location**: `client/web/tools/package-hnmo.sh:104-150`, `client/web/public/account.html:291-292, 841, 958`
   - **Repro**: PR `#29` and `#12` load `/js/accountPage.js`, `/js/siteLanguage.js`, `/js/deviceId.js`, and `/js/giftCodeIdentity.js` in `account.html`, which were deleted after bundling `main.js`, `website.js`, `topupPage.js`, and `calculator.js`.
   - **Fix Applied**: Added `esbuild` bundling for those 4 scripts to `*.min.js` and updated `account.html` rewrite rules in `package-hnmo.sh`.

### Medium

7. **Unbounded full-table account load on every `Ctrl+K` keystroke in AdminPanel**
   - **Location**: `server/src/Web/AdminPanel/Components/Layout/ConfigurationSearch.razor.cs:275-291`
   - **Repro**: `Ctrl+K` quick-search called `await context.GetAsync<Account>()` and filtered in memory on every query, loading the entire accounts table (including character trees) on larger databases.
   - **Fix Applied**: Switched to `persistence.CreateNewPlayerContext(config)` + bounded `context.SearchAccountsAsync(query, 0, 3)`.

8. **Sydney DST spring-forward invalid timestamp exception in `NewsStore.ParseSydneyToUtc`**
   - **Location**: `server/src/GameLogic/PlugIns/News/NewsStore.cs:84-103`
   - **Repro**: Scheduling a news post during an invalid spring-forward wall-clock minute (e.g. `2026-10-04T02:30` in `Australia/Sydney`) caused `TimeZoneInfo.ConvertTimeToUtc` to throw `ArgumentException`.
   - **Fix Applied**: Checks `zone.IsInvalidTime(unspecified)` and advances `+1h` before converting to UTC. Added `NewsPlugInTests.ParseSydneyToUtcHandlesDstSpringForwardGapSafely`.

9. **Newly created `Stats.IsVip` attribute not attached to `GameConfiguration.Attributes`**
   - **Location**: `server/src/Web/AdminPanel.Operations/ServerSettingsOperations.cs:155-171`
   - **Repro**: If `Stats.IsVip` did not yet exist when toggling VIP-only on `/servers`, `ApplyVipRequirementAsync` created the `AttributeDefinition` without adding it to `gc.Attributes`.
   - **Fix Applied**: Attached newly created `vipAttr` to `gc?.Attributes`.

10. **Integration UI/Help/Catalog gaps between `#33`, `#82`, `#83`, `#29`, and `#79–#81`**
    - **Locations**:
      - `server/src/Web/AdminPanel/Pages/BanManager.razor:12-30` (`.alert-info` rendered outside `.page-help`)
      - `server/src/Web/AdminPanel/Pages/AdminGuide.razor:10-46` (toggle outside `.page-help`; filter/clear buttons lacked `title`)
      - `server/src/Web/AdminPanel/Pages/MerchantShops.razor:12-18` (`.shop-help` / `#shop-help-box` lacked `.page-help` wrapper)
      - `server/src/Web/AdminPanel/Pages/NewsManager.razor:10-28, 76, 130-136` (toggle outside `.page-help`; form action buttons lacked `title`)
      - `server/src/Web/AdminPanel/Services/AdminNavCatalog.cs` (missing 8 parameterless routes: `bans`, `character-tools`, `gm`, `vip`, `merchants/legacy`, `gift-box`, `news`, `email-settings`)
      - `server/tests/HNMO.Web.Tests/Pages/EveryAdminPageHelpTests.cs:300-306` (`EmailSettings` needed `AddDataProtection()` + `WebsiteMailSettingsStore`)
      - `server/src/Web/AdminPanel/Pages/ApiKeys.razor` & `client/web/src/net/session.ts` (clean merge conflict & duplicate import repair)
      - Localized `.resx` files across `AdminPanel`, `Shared`, `GameLogic`, `GameServer` (repaired duplicate keys, template example nodes, and missing EN/VI pairs).
    - **Fix Applied**: Resolved cleanly; `EveryAdminPageHelpTests`, `AdminRedesignTests`, and `WebsiteMailSettingsTests` pass 214/214.

### Low / Scope Notes

11. **In-memory `ResetEngine.Record` / `TopRacesPlugIn.ObserveCharacter` executed before `SaveChangesAsync`**
    - **Location**: `server/src/Web/AdminPanel/API/WebsiteResetsController.cs:182-210, 421-475`
    - **Fix Applied**: Deferred `engine.Record` and `TopRacesPlugIn.ObserveCharacter` into a post-save callback executed only after `context.SaveChangesAsync()` succeeds.

12. **PR `#8` (`chatgpt-editor-vi`) C++ editor & Python check script**
    - **Location**: `client/src/MuEditor/**`, `client/tools/check-editor-vi.py`, `.github/workflows/editor-localization.yml`
    - **Status**: Left untouched on branch history as legacy reference; HNMO web runtime uses only `client/web`, AdminPanel (`/world-editor`, `/map-editor`, `/terrain-editor`, `/item-definitions`), and `server/`.

13. **Leftover pre-existing StyleCop warnings in untouched/partially merged files**
    - Observed SA1402/SA1202/SA1210/CS1570 warnings in existing AdminPanel/Persistence files (`HostSettingsService.cs`, `WebsiteMarketController.cs`, `MissionsRewards.razor.cs`, `MarketControllerTests.cs`); no new warnings introduced in edited/new C# files.

14. **Missing `[NotExposedToAgents]` / `internal` test-hook scoping on Moderation, Character Tools & GM Operations (`OperationParityTests`)**
    - **Location**: `server/src/Web/AdminPanel.Operations/BanAdminOperations.cs`, `CharacterToolsOperations.cs`, `GmManagerOperations.cs`
    - **Repro**: `OperationParityTests.AllOperationsAreExposedWithAtLeastTheirRequiredRole` inspects every public member on `*Operations` types and failed on `BanAdminOperations.SearchAsync` and public test override properties (`ActivityLogFileName`, `StoreOverride`).
    - **Fix Applied**: Changed test-only `ActivityLogFileName` / `StoreOverride` properties to `internal` (accessible via `InternalsVisibleTo("HNMO.Web.Tests")`) and marked all UI-only public methods on `BanAdminOperations`, `CharacterToolsOperations`, and `GmManagerOperations` with `[NotExposedToAgents(...)]`. Verified `HNMO.Web.Tests` passes `1357/1357`.

15. **Missing EN/VI language switcher on `guide.html` after `#83` (`public-vietnamese.spec.ts`)**
    - **Location**: `client/web/public/guide.html:6-41, 226`, `client/web/tools/package-hnmo.sh:142`
    - **Repro**: `#83` (`chatgpt-admin-redesign`) stripped host sections from an older `guide.html` copy that lacked `#4`'s `[data-site-language]` switcher and `<script type="module" src="/js/siteLanguage.js"></script>`.
    - **Fix Applied**: Kept the player-only guide sections from `#83` while restoring `[data-site-language]` EN/VI buttons, `<body data-page-label="Guide">`, and `/js/siteLanguage.js`, plus updating `package-hnmo.sh` to rewrite `guide.html` to `/js/siteLanguage.min.js`.

16. **Missing `describeServerChoice` & `fetchServerMetadata` export in `main.ts` (`vip-servers.spec.ts`)**
    - **Location**: `client/web/src/main.ts:963-1055`
    - **Repro**: `main.ts` kept only `fetchWorldLabels()` during the `#39` merge, dropping `export function describeServerChoice(...)` and VIP/Drop/Zen server badge metadata.
    - **Fix Applied**: Restored `WebsiteServerMeta`, `fetchServerMetadata()`, and exported `describeServerChoice()` preserving both multi-world labels (`worlds`) and VIP badges / rate metadata (`servers`).

---

## 3. Verification Summary on `chatgpt-integration` (Windows `win-x64`)

### Builds
- **`client/web`: `npm run typecheck && npm run build`**: `0` errors (`world-editor.js` `904.1kb`).
- **`dotnet build server/src/HNMO.sln -c Release`**: `0` errors.
- **`client/web/tools/package-hnmo.sh ... win-x64`**: exited `0` (initial full pack `1,845` files / `1,286 MB` with `48` `.hpk` packs / `14,482` entries; refreshed after `guide.html` / `main.ts` edits with `--no-data`).

### `.NET` Test Suite (`-c Release`)
- **`HNMO.Web.Tests.dll`**: **1,357 passed, 0 failed, 0 skipped** (`52 s`, includes `OperationParityTests`, `EveryAdminPageHelpTests`, `AdminRedesignTests`, `WebsiteMailSettingsTests`, and all ChatGPT AdminPanel page tests; 1 skipped baseline case in output: `AppliedConfigurationIsImmediatelyReadableAsync`).
- **`HNMO.AttributeSystem.Tests.dll`**: **49 passed, 0 failed, 0 skipped**.
- **`HNMO.ChatServer.Tests.dll`**: **26 passed, 0 failed, 0 skipped**.
- **`HNMO.ConnectServer.Tests.dll`**: **8 passed, 0 failed, 0 skipped**.
- **`HNMO.Network.Packets.Tests.dll`**: **782 passed, 0 failed, 0 skipped**.
- **`HNMO.Network.Tests.dll`**: **75 passed, 0 failed, 4 skipped**.
- **`HNMO.Pathfinding.Tests.dll`**: **10 passed, 0 failed, 0 skipped**.
- **`HNMO.Persistence.Initialization.Tests.dll`**: **858 passed, 0 failed, 12 skipped** (`8 m 30 s`).
- **`HNMO.PlugIns.Tests.dll`**: **55 passed, 0 failed, 0 skipped**.
- **`HNMO.Tests.dll`**:
  - Targeted post-edit run (`BanManagerEnforcementTests|NewsPlugInTests`): **9 passed, 0 failed, 0 skipped**.
  - Baseline full `HNMO.Tests.dll` run before `BanManagerEnforcementTests` class fix: **3,349 passed, 2 skipped, 4 failed** (1 fixed test case in `BanManagerEnforcementTests` + 3 pre-existing German `Discord` tests failing on this Windows host because Windows Application Control `0x800711C7` blocked loading `de/HNMO.Discord.resources.dll`; `vi/HNMO.Discord.resources.dll` loaded cleanly).

### Playwright E2E Verification (`--workers=1`)
- **Refreshed packaged `HNMO-pkg` (`18080/18095/54406/55433`) post-final-edit smoke suite**: **17/17 passed, 0 failed (`5.1m`)** (`admin-redesign`, `build-calculator`, `character`, `public-vietnamese` x6, `quests` x3, `touch-login`, `touch` x2, `website`), with `AdminPanel / -> 200`.
- **Packaged HTTP route smoke check (`18080` & `18095`)**: `200 OK` on `/`, `/bans`, `/gift-box`, `/news`, `/vip`, `/servers`, `/admin-guide`, `/character-tools`, `/gm`, `/email-settings`, `/merchants`, `/merchants/legacy`, `/account.html`, and all bundled `.min.js` entrypoints.
- **Unbundled dev web server (`18081`) ChatGPT + HUD + Control suite (65 tests)**: **63 passed, 2 failed** on first run (`control.spec.ts` lacked `DOTNET_ROOT` on `PATH`, `vip-servers.spec.ts:89` missing `describeServerChoice`); both fixed and verified in post-fix re-run (`control.spec.ts` + `vip-servers.spec.ts`: **3/3 passed, `12.9s`**).
