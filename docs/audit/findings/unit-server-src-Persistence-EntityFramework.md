# Unit: server/src/Persistence/EntityFramework

Checked against: neither the Season 6 suite nor the Season 9 suite defines EF Core schemas or custom PostgreSQL runtime tables; the Season 6 and Season 9 suites store accounts/characters/items/cashshop/siege state in SQL Server stored procedures (`same/differs` noted against suite database roles; `HNMO custom` for cross-season PostgreSQL persistence rules).

Scope: `server/src/Persistence/EntityFramework` (`251` unit files, `30984` lines; includes root `DbContext`s, `Postgres*` stores, `AdminAuth/`, `Extensions/`, `Json/`, `Loading/`, `Model/`, `TypedContexts/`, plus generated `CompiledModels/` and `Migrations/`).

## Summary

- EF Core contexts, generated entity models, model-builder extensions, JSON loaders, migrations, compiled models, and `AdminAuth` / `DatabaseSnapshotService` are persistence plumbing (`No game rules` in `AccountContext.cs:1-32`, `TradeContext.cs:1-33`, `EntityDataContext.cs:1-100`, `AdminAuth/AdminUserRepository.cs:22-32`, `AdminAuth/ApiKeyRepository.cs:61-64`, `DatabaseSnapshotService.cs:29-45`).
- Custom PostgreSQL runtime stores enforce HNMO economic, anti-dupe, cross-season arena, and audit retention guards: non-negative coin clamp on shared accounts, UUID item serial uniqueness assignment, transactional daily arena point cap with `FOR UPDATE`, cross-season login license window (`2m`), and archive-before-delete log purging.

## Findings

| File & Line | Rule / Enforcement | Suite Role & Value | Result |
|---|---|---|---|
| `PostgresSharedAccountStore.cs:1-66` | Shared `data."Account"` store clamps `WCoinC`, `WCoinP`, `GoblinPoints` to `>= 0` via `GREATEST(0, ...)` and excludes template/bot accounts | Suite CashShop/WebMall coin columns allow non-negative balances; cross-season shared Postgres store is `HNMO custom` | HNMO custom (safe floor `>= 0`) |
| `PostgresItemSerialDatabase.cs:1-76` | Assigns unique UUID `Serial` to non-merchant `data."Item"` rows and checks duplicate `Serial`s | Suite item serial generator (`WZ_GetItemSerial`) assigns sequential integer serials to prevent dupes; UUID anti-dupe guard in `HNMO` | Equivalent anti-dupe role / differs format (UUID) |
| `PostgresArenaShared.cs:71-130` | `AddPoints` locks `HnmoArenaAccount` `FOR UPDATE`, resets `EarnedToday` when `EarnedDay != dayKey`, clamps earned amount to `dailyCap - earnedToday`; `TrySpendPoints` enforces `"Points" >= @points` | Suite has no cross-season Arena Points table | HNMO custom |
| `PostgresArenaShared.cs:176-237` | `GetRating` caches ratings for `1` minute; `Schedules` filters active arena schedules to `UpdatedUtc > now - 10m` | Suite has no cross-season Arena table | HNMO custom |
| `PostgresArenaShared.cs:259-279` | `ClaimGuildXp` claims up to `max` queued rewards atomically with `FOR UPDATE SKIP LOCKED` | Suite has no cross-season guild XP queue | HNMO custom |
| `PostgresArenaShared.cs:341-348` | `LicensedElsewhere` flags logins active in another season within the last `2` minutes | Suite is single-season | HNMO custom |
| `PostgresSharedLoginTicketStore.cs:1-64` | `UNLOGGED TABLE public.hnmo_login_ticket` enforces one-time hash ticket redemption with `expires_at` expiry | Suite has no launcher SSO ticket table | HNMO custom |
| `PostgresDbLogRetentionStore.cs:116-200` | `ExportAndPurgePostgres` deletes `hnmo.chat_log` and `admin."AdminAuditEntry"` rows strictly after writing a verified non-empty `.json.gz` export | Suite logs plain text / DB rows without gzip purge gate | HNMO custom |
| `PostgresHnmoPlayerStateRowBackend.cs:1-100` | Stores/archives per-scope JSONB rows in `hnmo.player_feature_state` and `hnmo.archived_feature_state` | Persistence storage backend | Infrastructure |
| `PersistenceContextProvider.cs:101-293` | Database schema migration wait polls every `3000ms`; wires runtime PostgreSQL stores when connection string is configured | Persistence lifecycle plumbing | No game rules |
| `AccountContext.cs:1-32`, `TradeContext.cs:1-33`, `EntityDataContext.cs:1-100` | EF context ignore/DbSet mappings for accounts, trades, siege, Crywolf, CashShop, Gens | Schema mapping | No game rules |
| `AdminAuth/AdminUserRepository.cs:22-32`, `AdminAuth/ApiKeyRepository.cs:61-64`, `DatabaseSnapshotService.cs:29-45` | Admin DB timeouts (`3s`/`30s`/`30s`), disabled API key filter (`!k.IsDisabled`), v2 binary snapshot export | Admin/backup infrastructure | No game rules |
| `PlayerContext.cs:172-184,270-285`, `PostgresHnmoPlayerStateRowBackend.cs` | Re-check `8cb8539b`: `ReadEligibleAccountKeysAsync` & `ReadRankingOwnersAsync` exclude bots/templates/banned accounts (`Take(100)`); Postgres row store updates for retained history | Ranking & tracker privacy queries | No game rules |

## Re-check 2026-11-27 (changes 8cb8539b..a108e846)

Commits read (every changed file read in full):

- 004b40c3 Coins: take back only what the shared store really took

Evidence:

- PostgresSharedAccountStore.AddCoinsAndReportAsync: one statement (CTE with FOR UPDATE + UPDATE ... RETURNING old and new) so "before" belongs to exactly this change - fixed (#407).
