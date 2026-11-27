// <copyright file="PostgresSharedAccountStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.EntityFramework;

using System.Threading;
using HNMO.DataModel.Entities;
using Npgsql;

/// <summary>The shared accounts and coins in the shared (Season 6.3) PostgreSQL database.</summary>
/// <param name="connectionString">The connection string of the shared database.</param>
public sealed class PostgresSharedAccountStore(string connectionString) : ICoordinatedSharedCoinStore
{
    /// <inheritdoc />
    public async ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "LoginName", "PasswordHash", "SecurityCode", "EMail", "State", "LanguageIsoCode", "ChatBanUntil", "WCoinC", "WCoinP", "GoblinPoints"
            FROM data."Account" WHERE lower("LoginName") = lower(@login) AND NOT "IsTemplate" AND NOT "IsBot" LIMIT 1;
            """;
        command.Parameters.AddWithValue("login", loginName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        async ValueTask<string?> TextAsync(int column) =>
            await reader.IsDBNullAsync(column, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(column);
        return new SharedAccount(
            reader.GetString(0),
            await TextAsync(1).ConfigureAwait(false) ?? string.Empty,
            await TextAsync(2).ConfigureAwait(false) ?? string.Empty,
            await TextAsync(3).ConfigureAwait(false) ?? string.Empty,
            (AccountState)reader.GetInt32(4),
            await TextAsync(5).ConfigureAwait(false),
            await reader.IsDBNullAsync(6, cancellationToken).ConfigureAwait(false) ? null : reader.GetDateTime(6),
            new CoinBalances(reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9)));
    }

    /// <inheritdoc />
    public async ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE data."Account"
            SET "WCoinC" = GREATEST(0, "WCoinC" + @c), "WCoinP" = GREATEST(0, "WCoinP" + @p), "GoblinPoints" = GREATEST(0, "GoblinPoints" + @g)
            WHERE lower("LoginName") = lower(@login) AND NOT "IsTemplate" AND NOT "IsBot"
            RETURNING "WCoinC", "WCoinP", "GoblinPoints";
            """;
        command.Parameters.AddWithValue("login", loginName);
        command.Parameters.AddWithValue("c", change.WCoinC);
        command.Parameters.AddWithValue("p", change.WCoinP);
        command.Parameters.AddWithValue("g", change.GoblinPoints);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new CoinBalances(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2))
            : null;
    }

    /// <inheritdoc />
    public async ValueTask<(CoinBalances Before, CoinBalances After)?> AddCoinsAndReportAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        // One statement: the old row is locked and read, then updated, so "before" belongs to exactly this change.
        command.CommandText = """
            WITH old AS (
                SELECT "Id", "WCoinC", "WCoinP", "GoblinPoints" FROM data."Account"
                WHERE lower("LoginName") = lower(@login) AND NOT "IsTemplate" AND NOT "IsBot"
                FOR UPDATE)
            UPDATE data."Account" AS a
            SET "WCoinC" = GREATEST(0, a."WCoinC" + @c), "WCoinP" = GREATEST(0, a."WCoinP" + @p), "GoblinPoints" = GREATEST(0, a."GoblinPoints" + @g)
            FROM old WHERE a."Id" = old."Id"
            RETURNING old."WCoinC", old."WCoinP", old."GoblinPoints", a."WCoinC", a."WCoinP", a."GoblinPoints";
            """;
        command.Parameters.AddWithValue("login", loginName);
        command.Parameters.AddWithValue("c", change.WCoinC);
        command.Parameters.AddWithValue("p", change.WCoinP);
        command.Parameters.AddWithValue("g", change.GoblinPoints);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (new CoinBalances(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)), new CoinBalances(reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5)))
            : null;
    }

    /// <inheritdoc />
    public async ValueTask<PreparedCoinTransaction?> BeginPreparedCoinChangeAsync(
        string loginName,
        CoinBalances change,
        string gid,
        long operationLockKey,
        CancellationToken cancellationToken = default)
    {
        if (!SharedCoinGid.IsValid(gid))
        {
            throw new ArgumentException($"Invalid prepared transaction id: {gid}", nameof(gid));
        }

        var opLock = await this.TryLockOperationAsync(operationLockKey, cancellationToken).ConfigureAwait(false);
        if (opLock is null)
        {
            throw new InvalidOperationException($"Another worker already holds the coordinator lock for {gid}.");
        }

        var keepLock = false;
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using (var begin = new NpgsqlCommand("BEGIN; SET LOCAL lock_timeout = '5000ms';", connection))
            {
                await begin.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            CoinBalances? before = null;
            CoinBalances? after = null;
            await using (var update = connection.CreateCommand())
            {
                // A fee the shared balance cannot cover is refused in full (all coin columns stay non-negative).
                update.CommandText = """
                    WITH old AS (
                        SELECT "Id", "WCoinC", "WCoinP", "GoblinPoints" FROM data."Account"
                        WHERE lower("LoginName") = lower(@login) AND NOT "IsTemplate" AND NOT "IsBot"
                        FOR UPDATE)
                    UPDATE data."Account" AS a
                    SET "WCoinC" = a."WCoinC" + @c,
                        "WCoinP" = a."WCoinP" + @p,
                        "GoblinPoints" = a."GoblinPoints" + @g
                    FROM old
                    WHERE a."Id" = old."Id"
                      AND a."WCoinC" + @c >= 0
                      AND a."WCoinP" + @p >= 0
                      AND a."GoblinPoints" + @g >= 0
                    RETURNING old."WCoinC", old."WCoinP", old."GoblinPoints", a."WCoinC", a."WCoinP", a."GoblinPoints";
                    """;
                update.Parameters.AddWithValue("login", loginName);
                update.Parameters.AddWithValue("c", change.WCoinC);
                update.Parameters.AddWithValue("p", change.WCoinP);
                update.Parameters.AddWithValue("g", change.GoblinPoints);
                await using var reader = await update.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    before = new CoinBalances(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
                    after = new CoinBalances(reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5));
                }
            }

            if (before is null || after is null)
            {
                await using var rollback = new NpgsqlCommand("ROLLBACK", connection);
                await rollback.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            await AssetOperationTestBarrier.ReachAsync("before-prepare", cancellationToken).ConfigureAwait(false);
            await using (var prepare = new NpgsqlCommand($"PREPARE TRANSACTION '{gid}'", connection))
            {
                await prepare.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await AssetOperationTestBarrier.ReachAsync("after-prepare", cancellationToken).ConfigureAwait(false);
            keepLock = true;
            return new PreparedCoinTransaction(gid, before.Value, after.Value, opLock);
        }
        finally
        {
            if (!keepLock)
            {
                await opLock.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask CommitPreparedAsync(string gid, CancellationToken cancellationToken = default)
    {
        if (!SharedCoinGid.IsValid(gid))
        {
            throw new ArgumentException($"Invalid prepared transaction id: {gid}", nameof(gid));
        }

        await AssetOperationTestBarrier.ReachAsync("before-canonical-commit", cancellationToken).ConfigureAwait(false);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var cmd = new NpgsqlCommand($"COMMIT PREPARED '{gid}'", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == "42704")
        {
            // Already committed or rolled back by another pass.
        }

        await AssetOperationTestBarrier.ReachAsync("after-canonical-commit", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RollbackPreparedAsync(string gid, CancellationToken cancellationToken = default)
    {
        if (!SharedCoinGid.IsValid(gid))
        {
            throw new ArgumentException($"Invalid prepared transaction id: {gid}", nameof(gid));
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var cmd = new NpgsqlCommand($"ROLLBACK PREPARED '{gid}'", connection);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == "42704")
        {
            // Already resolved.
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListPreparedTransactionsAsync(string realmId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT gid FROM pg_prepared_xacts
            WHERE database = current_database() AND gid LIKE @pattern ESCAPE '!'
            ORDER BY prepared;
            """;
        cmd.Parameters.AddWithValue("pattern", SharedCoinGid.LikePatternForRealm(realmId));
        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable?> TryAcquireRecoveryLockAsync(string realmId, CancellationToken cancellationToken = default)
        => this.TryAdvisoryLockAsync(SharedCoinGid.RecoveryLockKey(realmId), cancellationToken);

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable?> TryLockOperationAsync(long operationLockKey, CancellationToken cancellationToken = default)
        => this.TryAdvisoryLockAsync(operationLockKey, cancellationToken);

    private async ValueTask<IAsyncDisposable?> TryAdvisoryLockAsync(long key, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT pg_try_advisory_lock(@key);";
            cmd.Parameters.AddWithValue("key", key);
            var acquired = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
            if (!acquired)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                return null;
            }

            return new AdvisoryLockLease(connection, key);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// A held session advisory lock. It is unlocked explicitly: closing the connection only gives it back to the pool,
    /// where the session (and its lock) lives on until the connection is used again, so the next recovery found the
    /// operation or the realm "busy" and skipped it (a transaction kept pending while the local database was unreadable
    /// then stayed prepared).
    /// </summary>
    private sealed class AdvisoryLockLease(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT pg_advisory_unlock(@key);";
                cmd.Parameters.AddWithValue("key", key);
                await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
            {
                // The connection is gone, and with it the session and its lock: clear it from the pool so it isn't reused.
                NpgsqlConnection.ClearPool(connection);
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
