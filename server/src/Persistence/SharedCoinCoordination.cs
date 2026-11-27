// <copyright file="SharedCoinCoordination.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using HNMO.Interfaces;

/// <summary>
/// Resolves the realm identifier embedded into canonical two-phase prepared transaction IDs so multiple
/// secondary seasons sharing one canonical Season 6.3 database only recover their own transactions.
/// </summary>
public static class SharedCoinRealm
{
    /// <summary>Environment variable specifying a distinct realm id (defaults to <see cref="HnmoSeasonInstance.Current"/>).</summary>
    public const string EnvironmentVariableName = "HNMO_REALM_ID";

    /// <summary>Gets the current process realm identifier.</summary>
    public static string Current => Resolve(Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>Returns a normalized realm identifier (<c>[a-z0-9_-]</c>, max 48 chars).</summary>
    /// <param name="realmId">Optional explicit realm identifier.</param>
    /// <returns>The normalized realm identifier.</returns>
    public static string Resolve(string? realmId = null)
    {
        var raw = string.IsNullOrWhiteSpace(realmId) ? HnmoSeasonInstance.Current : realmId.Trim();
        var cleaned = Regex.Replace(raw.ToLowerInvariant(), @"[^a-z0-9_-]", "-").Trim('-');
        if (cleaned.Length == 0)
        {
            cleaned = HnmoSeasonInstance.Current;
        }

        return cleaned.Length > 48 ? cleaned[..48] : cleaned;
    }
}

/// <summary>
/// Builds and validates PostgreSQL <c>PREPARE TRANSACTION</c> identifiers (<c>gid</c>) and deterministic
/// 64-bit advisory lock keys for shared coin two-phase coordination.
/// </summary>
public static partial class SharedCoinGid
{
    private const string Prefix = "hnmo";

    [GeneratedRegex(@"^hnmo:[a-z0-9_-]{1,48}:[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex GidRegex();

    /// <summary>Builds the canonical GID for <paramref name="realmId"/> and <paramref name="operationId"/>.</summary>
    /// <param name="realmId">The realm identifier.</param>
    /// <param name="operationId">The local <c>AssetOperation.Id</c>.</param>
    /// <returns>The validated GID.</returns>
    public static string Build(string realmId, Guid operationId)
    {
        var realm = SharedCoinRealm.Resolve(realmId);
        return $"{Prefix}:{realm}:{operationId:N}";
    }

    /// <summary>Builds the <c>LIKE</c> prefix for prepared transactions belonging to <paramref name="realmId"/>.</summary>
    /// <param name="realmId">The realm identifier.</param>
    /// <returns>The LIKE pattern using <c>!</c> as the escape character.</returns>
    public static string LikePatternForRealm(string realmId)
    {
        var realm = SharedCoinRealm.Resolve(realmId)
            .Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal);
        return $"{Prefix}:{realm}:%";
    }

    /// <summary>Validates that <paramref name="gid"/> strictly matches the coordinator GID format.</summary>
    /// <param name="gid">The prepared transaction identifier.</param>
    /// <returns><c>true</c> if valid.</returns>
    public static bool IsValid(string gid) => !string.IsNullOrEmpty(gid) && GidRegex().IsMatch(gid);

    /// <summary>Parses <paramref name="gid"/> into its realm identifier and operation identifier.</summary>
    /// <param name="gid">The prepared transaction identifier.</param>
    /// <param name="realmId">The extracted realm identifier.</param>
    /// <param name="operationId">The extracted operation identifier.</param>
    /// <returns><c>true</c> when <paramref name="gid"/> is well-formed.</returns>
    public static bool TryParse(string gid, out string realmId, out Guid operationId)
    {
        realmId = string.Empty;
        operationId = Guid.Empty;
        if (!IsValid(gid))
        {
            return false;
        }

        var parts = gid.Split(':');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[2], "N", out operationId))
        {
            return false;
        }

        realmId = parts[1];
        return true;
    }

    /// <summary>Computes a deterministic 64-bit PostgreSQL advisory lock key for one prepared operation.</summary>
    /// <param name="gid">The GID.</param>
    /// <returns>A 64-bit lock key.</returns>
    public static long OperationLockKey(string gid) => HashToInt64($"op:{gid}");

    /// <summary>Computes a deterministic 64-bit PostgreSQL advisory lock key for realm recovery.</summary>
    /// <param name="realmId">The realm identifier.</param>
    /// <returns>A 64-bit lock key.</returns>
    public static long RecoveryLockKey(string realmId) => HashToInt64($"recover:{SharedCoinRealm.Resolve(realmId)}");

    private static long HashToInt64(string text)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(text), hash);
        return BitConverter.ToInt64(hash[..8]);
    }
}

/// <summary>
/// Represents an active prepared coin mutation held in the canonical Season 6.3 database until either
/// <c>COMMIT PREPARED</c> or <c>ROLLBACK PREPARED</c> resolves it. Disposing releases the worker's
/// in-flight session advisory lock.
/// </summary>
/// <param name="Gid">The prepared transaction GID.</param>
/// <param name="Before">The canonical balances immediately before the prepared change.</param>
/// <param name="After">The canonical balances produced by the prepared change.</param>
/// <param name="OperationLock">The worker session lock lease preventing premature recovery while active.</param>
public sealed record PreparedCoinTransaction(
    string Gid,
    CoinBalances Before,
    CoinBalances After,
    IAsyncDisposable OperationLock) : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync() => this.OperationLock.DisposeAsync();
}

/// <summary>
/// Extends <see cref="ISharedAccountStore"/> with PostgreSQL two-phase commit (<c>PREPARE TRANSACTION</c>)
/// and advisory lock recovery primitives for durable shared-coin coordination across databases.
/// </summary>
public interface ICoordinatedSharedCoinStore : ISharedAccountStore
{
    /// <summary>
    /// Prepares a coin delta on the canonical account inside a PostgreSQL prepared transaction.
    /// Any fee that would take <c>WCoinC</c>, <c>WCoinP</c>, or <c>GoblinPoints</c> below zero is
    /// refused in full (returns <c>null</c> without preparing or partial debiting).
    /// </summary>
    /// <param name="loginName">Account login name.</param>
    /// <param name="change">Coin balance delta.</param>
    /// <param name="gid">Validated prepared transaction identifier (<see cref="SharedCoinGid.Build"/>).</param>
    /// <param name="operationLockKey">Advisory lock key held for the duration of the worker's attempt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The prepared handle, or <c>null</c> if the account is missing or cannot cover the full fee.</returns>
    ValueTask<PreparedCoinTransaction?> BeginPreparedCoinChangeAsync(
        string loginName,
        CoinBalances change,
        string gid,
        long operationLockKey,
        CancellationToken cancellationToken = default);

    /// <summary>Commits an existing prepared transaction on the canonical database idempotently.</summary>
    /// <param name="gid">The prepared transaction identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    ValueTask CommitPreparedAsync(string gid, CancellationToken cancellationToken = default);

    /// <summary>Rolls back an existing prepared transaction on the canonical database idempotently.</summary>
    /// <param name="gid">The prepared transaction identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    ValueTask RollbackPreparedAsync(string gid, CancellationToken cancellationToken = default);

    /// <summary>Lists all currently prepared transaction GIDs belonging to <paramref name="realmId"/>.</summary>
    /// <param name="realmId">The realm identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered GIDs in <c>pg_prepared_xacts</c> for this database and realm.</returns>
    ValueTask<IReadOnlyList<string>> ListPreparedTransactionsAsync(string realmId, CancellationToken cancellationToken = default);

    /// <summary>Attempts to acquire the cluster-wide non-blocking advisory lock for recovering <paramref name="realmId"/>.</summary>
    /// <param name="realmId">The realm identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async-disposable lock handle, or <c>null</c> if another recovery pass holds it.</returns>
    ValueTask<IAsyncDisposable?> TryAcquireRecoveryLockAsync(string realmId, CancellationToken cancellationToken = default);

    /// <summary>Attempts to acquire the non-blocking advisory lock for one operation key during recovery.</summary>
    /// <param name="operationLockKey">The operation lock key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async-disposable lock handle, or <c>null</c> if a live worker still holds it.</returns>
    ValueTask<IAsyncDisposable?> TryLockOperationAsync(long operationLockKey, CancellationToken cancellationToken = default);
}
