# Audit Findings: `server/src/Persistence/EntityFramework`

- **Unit**: `server/src/Persistence/EntityFramework`
- **Checked on**: 2026-11-24
- **Status**: `Checked`

## Summary

Audited the persistence logic, PostgreSQL stores, EF Core contexts, repositories, JSON object loaders, snapshot service, and admin auth storage in `server/src/Persistence/EntityFramework` (excluding EF-generated `CompiledModels` and `Migrations`).

## Key Verifications

1. **HNMO PostgreSQL Stores**:
   - `PostgresHnmoPlayerStateRowBackend`: Parameterized `NpgsqlBatch` upserts/deletes, escaped `LIKE` prefix paging, and transactional `WITH moved AS (DELETE ... RETURNING ...) INSERT` archival in `ArchiveExpired`.
   - `PostgresArenaShared`: `AddPoints` enforces daily caps under `FOR UPDATE` row locks; `ClaimGuildXp` uses `FOR UPDATE SKIP LOCKED`; `PublishOnline` and `PublishLicensed` prune stale entries atomically.
   - `PostgresDbLogRetentionStore`: Writes and verifies non-empty `.json.gz` archives via atomic `File.Move` before deleting expired chat/audit rows in a transaction.
   - `PostgresSharedLoginTicketStore`: Atomic one-shot ticket redemption via `DELETE ... WHERE hash = @hash RETURNING ...`.
   - `PostgresItemSerialDatabase`: Assigns missing `Serial` UUIDs excluding merchant stores and reports duplicates.
2. **EF Core Context & Repositories**:
   - `EntityFrameworkContextBase`: Bounded 3-attempt retry for transient concurrency conflicts (`IsTransientConcurrencyConflict`, excluding `ObjectDisposedException`), classifies DB rejections as `PersistenceConflictException`, and `ForEachAggregate` stops at `SharedConfigurationTypes` during `DeleteAsync`.
   - `PlayerContext` / `AccountRepository` / `ItemStorageRepository`: Parameterized queries, BCrypt verification, atomic `TryLinkAccountAsync` transaction, and `Invariant` lowercasing for .NET-side search terms.
   - `DatabaseSnapshotService`: Binary `COPY` per table with manifest migration compatibility checks and `session_replication_role = replica` during restore.
3. **Admin Auth (`AdminAuth/`)**:
   - `AdminUserRepository.TryAdvanceTotpStepAsync`: Conditional `ExecuteUpdateAsync` (`LastAcceptedTotpStep < step`) prevents TOTP replay under concurrent requests.
   - `AdminPanelContext`: Isolated `admin` schema and migration history.
4. **Related Cross-Check (`HnmoFeatureRowSync<T>` in `server/src/Persistence/HnmoFeatureRowSync{T}.cs`)**:
   - Verified that `SaveChanges(..., useDirtySetOnly: true)` falls back to a full scan when `_dirtyKeys.Count == 0` (so stores that only call `Changed()` without `MarkDirty` still persist), though mixing `MarkDirty` with untracked `Changed()` on the same sync instance would skip the untracked key.
