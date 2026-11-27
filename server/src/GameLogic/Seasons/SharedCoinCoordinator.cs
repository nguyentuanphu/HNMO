// <copyright file="SharedCoinCoordinator.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Seasons;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using HNMO.DataModel.Entities;
using HNMO.Persistence;
using Microsoft.Extensions.Logging;

/// <summary>Outcome of a coordinated two-phase shared-coin operation.</summary>
public enum SharedCoinCommitOutcome
{
    /// <summary>Refused in full before commit (insufficient shared balance, missing account, or local refusal).</summary>
    Refused,

    /// <summary>Same operation identifier and payload were already committed locally earlier.</summary>
    AlreadyCommitted,

    /// <summary>Same operation identifier was previously committed with a different payload.</summary>
    Conflict,

    /// <summary>Both the canonical Season 6.3 database and the local Season 9 database committed.</summary>
    Committed,

    /// <summary>Local decision and entitlement committed durably; canonical <c>COMMIT PREPARED</c> awaits locked recovery.</summary>
    CanonicalCommitDeferred,
}

/// <summary>Result of <see cref="SharedCoinCoordinator.ExecuteAsync"/>.</summary>
/// <param name="Outcome">Coordinator outcome.</param>
/// <param name="Before">Canonical balances before the change (zero if not prepared).</param>
/// <param name="After">Canonical balances after the change (zero if refused).</param>
/// <param name="Gid">Canonical prepared transaction id used, or <c>null</c> if refused before prepare.</param>
public sealed record SharedCoinCommitResult(
    SharedCoinCommitOutcome Outcome,
    CoinBalances Before,
    CoinBalances After,
    string? Gid);

/// <summary>Summary of one locked recovery sweep across prepared transactions for a realm.</summary>
/// <param name="LockAcquired"><c>true</c> if this pass acquired the realm recovery lock.</param>
/// <param name="Committed">Prepared GIDs committed from a durable local <see cref="AssetOperation"/> receipt.</param>
/// <param name="RolledBack">Prepared GIDs rolled back because the local database confirmed no receipt existed.</param>
/// <param name="KeptPendingLocalUnreadable">Prepared GIDs kept pending because the local outcome could not be read.</param>
/// <param name="SkippedActive">Prepared GIDs skipped because a live worker still holds their operation lock.</param>
public sealed record SharedCoinRecoveryReport(
    bool LockAcquired,
    IReadOnlyList<string> Committed,
    IReadOnlyList<string> RolledBack,
    IReadOnlyList<string> KeptPendingLocalUnreadable,
    IReadOnlyList<string> SkippedActive);

/// <summary>
/// Durable two-phase coordinator between the canonical Season 6.3 coin store (owner of balances) and a secondary
/// Season 9 database (<c>PREPARE TRANSACTION</c> on canonical, local decision written in <see cref="AssetOperation"/>
/// in the same database transaction as the local entitlement, and locked recovery per realm).
/// </summary>
/// <remarks>
/// Arena points and ratings use <see cref="IArenaShared"/> (<c>data."HnmoArenaAccount"</c>), not
/// <see cref="ISharedAccountStore"/>, so Arena never mutates shared WCoin/GoblinPoint balances through this path.
/// </remarks>
public static class SharedCoinCoordinator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RecoveryGates = new(StringComparer.Ordinal);

    /// <summary>
    /// Executes a coordinated shared-coin change:
    /// <list type="number">
    /// <item>Validates and prepares <paramref name="canonicalDelta"/> on the canonical database (<c>PREPARE TRANSACTION</c>); insufficient balances are refused in full.</item>
    /// <item>Commits the local entitlement and durable <see cref="AssetOperation"/> decision together in one local database transaction.</item>
    /// <item>Commits the prepared transaction on the canonical database, or rolls it back if local commit did not happen.</item>
    /// </list>
    /// </summary>
    /// <param name="canonicalStore">Coordinated canonical Season 6.3 coin store.</param>
    /// <param name="localContext">Local Season 9 persistence context carrying any local entitlement changes.</param>
    /// <param name="createVerificationContext">Creates an independent local context to verify lost-answer commits.</param>
    /// <param name="account">The local mirrored account whose coin fields should track canonical balances.</param>
    /// <param name="operationId">Caller-chosen stable identifier for this intent.</param>
    /// <param name="canonicalDelta">Delta to apply atomically to the canonical account.</param>
    /// <param name="kind">Receipt kind stored in <see cref="AssetOperation.Kind"/> (e.g. <c>shared-coin-sync</c>).</param>
    /// <param name="source">Origin (<c>game</c>, <c>website</c>, etc.).</param>
    /// <param name="payloadHash">Stable payload hash for idempotency / conflict detection.</param>
    /// <param name="realmId">Optional explicit realm identifier; defaults to <see cref="SharedCoinRealm.Current"/>.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="beforeLocalSave">Optional delegate invoked after canonical <c>PREPARE</c> succeeds and before local commit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The coordinator result.</returns>
    public static async ValueTask<SharedCoinCommitResult> ExecuteAsync(
        ICoordinatedSharedCoinStore canonicalStore,
        IPlayerContext localContext,
        Func<IPlayerContext> createVerificationContext,
        Account account,
        Guid operationId,
        CoinBalances canonicalDelta,
        string kind,
        string source,
        string payloadHash,
        string? realmId = null,
        ILogger? logger = null,
        Func<CoinBalances, CoinBalances, ValueTask>? beforeLocalSave = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canonicalStore);
        ArgumentNullException.ThrowIfNull(localContext);
        ArgumentNullException.ThrowIfNull(createVerificationContext);
        ArgumentNullException.ThrowIfNull(account);

        var realm = SharedCoinRealm.Resolve(realmId);
        var gid = SharedCoinGid.Build(realm, operationId);
        var lockKey = SharedCoinGid.OperationLockKey(gid);

        var prepared = await canonicalStore.BeginPreparedCoinChangeAsync(account.LoginName, canonicalDelta, gid, lockKey, cancellationToken).ConfigureAwait(false);
        if (prepared is null)
        {
            return new SharedCoinCommitResult(SharedCoinCommitOutcome.Refused, default, default, null);
        }

        await using var lease = prepared;
        var detail = JsonSerializer.Serialize(new
        {
            decision = "commit",
            realmId = realm,
            gid,
            loginName = account.LoginName,
            delta = new { c = canonicalDelta.WCoinC, p = canonicalDelta.WCoinP, g = canonicalDelta.GoblinPoints },
            canonicalBefore = new { c = prepared.Before.WCoinC, p = prepared.Before.WCoinP, g = prepared.Before.GoblinPoints },
            canonicalAfter = new { c = prepared.After.WCoinC, p = prepared.After.WCoinP, g = prepared.After.GoblinPoints },
        });

        var preLocalCoins = CoinBalances.Of(account);
        prepared.After.ApplyTo(account);
        if (beforeLocalSave is not null)
        {
            await beforeLocalSave(prepared.Before, prepared.After).ConfigureAwait(false);
        }

        var request = new AssetOperationRequest(
            operationId,
            account.GetId(),
            kind,
            payloadHash,
            source,
            detail,
            []);

        AssetCommitStatus localStatus;
        try
        {
            localStatus = await localContext.SaveChangesWithAssetOperationAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Shared coin {Operation} ({Gid}): local commit failed or lost its answer; verifying receipt.", operationId, gid);
            using var check = createVerificationContext();
            var existing = await check.GetAssetOperationAsync(operationId, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                preLocalCoins.ApplyTo(account);
                await canonicalStore.RollbackPreparedAsync(gid, cancellationToken).ConfigureAwait(false);
                return new SharedCoinCommitResult(SharedCoinCommitOutcome.Refused, prepared.Before, preLocalCoins, gid);
            }

            if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
            {
                preLocalCoins.ApplyTo(account);
                await canonicalStore.RollbackPreparedAsync(gid, cancellationToken).ConfigureAwait(false);
                return new SharedCoinCommitResult(SharedCoinCommitOutcome.Conflict, prepared.Before, preLocalCoins, gid);
            }

            localContext.AcceptChangesOfCommittedOperation();
            localStatus = AssetCommitStatus.Committed;
        }

        if (localStatus is AssetCommitStatus.Refused or AssetCommitStatus.Conflict)
        {
            preLocalCoins.ApplyTo(account);
            await canonicalStore.RollbackPreparedAsync(gid, cancellationToken).ConfigureAwait(false);
            var mapped = localStatus == AssetCommitStatus.Conflict
                ? SharedCoinCommitOutcome.Conflict
                : SharedCoinCommitOutcome.Refused;
            return new SharedCoinCommitResult(mapped, prepared.Before, preLocalCoins, gid);
        }

        try
        {
            await canonicalStore.CommitPreparedAsync(gid, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Shared coin {Operation} ({Gid}): local receipt committed, canonical COMMIT PREPARED deferred to recovery.", operationId, gid);
            return new SharedCoinCommitResult(SharedCoinCommitOutcome.CanonicalCommitDeferred, prepared.Before, prepared.After, gid);
        }

        var outcome = localStatus == AssetCommitStatus.AlreadyCommitted
            ? SharedCoinCommitOutcome.AlreadyCommitted
            : SharedCoinCommitOutcome.Committed;
        return new SharedCoinCommitResult(outcome, prepared.Before, prepared.After, gid);
    }

    /// <summary>
    /// Runs one locked recovery sweep for <paramref name="realmId"/>: resolves each prepared canonical transaction
    /// belonging to the realm from its durable local <see cref="AssetOperation"/> receipt, committing committed
    /// operations, rolling back un-committed ones, and keeping any prepared transaction pending while the local
    /// database outcome cannot be read.
    /// </summary>
    /// <param name="canonicalStore">Canonical coin store.</param>
    /// <param name="createLocalContext">Creates a local database context to inspect <see cref="AssetOperation"/> receipts.</param>
    /// <param name="realmId">Realm identifier (defaults to <see cref="SharedCoinRealm.Current"/>).</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recovery report.</returns>
    public static async ValueTask<SharedCoinRecoveryReport> RecoverAsync(
        ICoordinatedSharedCoinStore canonicalStore,
        Func<IPlayerContext> createLocalContext,
        string? realmId = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canonicalStore);
        ArgumentNullException.ThrowIfNull(createLocalContext);

        var realm = SharedCoinRealm.Resolve(realmId);
        var processGate = RecoveryGates.GetOrAdd(realm, static _ => new SemaphoreSlim(1, 1));
        if (!await processGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SharedCoinRecoveryReport(false, [], [], [], []);
        }

        try
        {
            await using var recoveryLock = await canonicalStore.TryAcquireRecoveryLockAsync(realm, cancellationToken).ConfigureAwait(false);
            if (recoveryLock is null)
            {
                return new SharedCoinRecoveryReport(false, [], [], [], []);
            }

            var preparedGids = await canonicalStore.ListPreparedTransactionsAsync(realm, cancellationToken).ConfigureAwait(false);
            var committed = new List<string>();
            var rolledBack = new List<string>();
            var keptPending = new List<string>();
            var skippedActive = new List<string>();

            foreach (var gid in preparedGids)
            {
                if (!SharedCoinGid.TryParse(gid, out var parsedRealm, out var operationId)
                    || !string.Equals(parsedRealm, realm, StringComparison.Ordinal))
                {
                    continue;
                }

                await using var opLock = await canonicalStore.TryLockOperationAsync(SharedCoinGid.OperationLockKey(gid), cancellationToken).ConfigureAwait(false);
                if (opLock is null)
                {
                    skippedActive.Add(gid);
                    continue;
                }

                DataModel.Entities.AssetOperation? receipt;
                try
                {
                    using var local = createLocalContext();
                    receipt = await local.GetAssetOperationAsync(operationId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger?.LogWarning(ex, "Shared coin recovery ({Realm}): local decision for {Gid} cannot be read; keeping prepared.", realm, gid);
                    keptPending.Add(gid);
                    continue;
                }

                if (receipt is not null)
                {
                    await canonicalStore.CommitPreparedAsync(gid, cancellationToken).ConfigureAwait(false);
                    committed.Add(gid);
                }
                else
                {
                    await canonicalStore.RollbackPreparedAsync(gid, cancellationToken).ConfigureAwait(false);
                    rolledBack.Add(gid);
                }
            }

            return new SharedCoinRecoveryReport(true, committed, rolledBack, keptPending, skippedActive);
        }
        finally
        {
            processGate.Release();
        }
    }
}
