// <copyright file="AssetOperationCommitter.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using HNMO.Persistence;

/// <summary>
/// Commits the asset operations of a player (<see cref="IPlayerContext.SaveChangesWithAssetOperationAsync"/>) and
/// settles a commit whose answer was lost by reading its receipt with a context of its own.
/// </summary>
public static class AssetOperationCommitter
{
    private const int OutcomeReadAttempts = 6;

    /// <summary>The accounts with an operation whose outcome couldn't be read (the database was unreachable).</summary>
    private static readonly ConcurrentDictionary<Guid, Guid> UncertainOperations = new();

    /// <summary>Gets or sets the delay between reads of a lost outcome (tests make it short).</summary>
    internal static TimeSpan OutcomeReadDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>A short, stable hash of what an operation does; another payload under the same id is a conflict.</summary>
    /// <param name="payload">The payload text.</param>
    /// <returns>The hash.</returns>
    public static string PayloadHash(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    /// <summary>Gets a value indicating whether an operation of the account has an outcome nobody could read yet.</summary>
    /// <param name="accountId">The account.</param>
    /// <returns><c>true</c> while the account's asset operations must wait.</returns>
    public static bool IsUncertain(Guid accountId) => UncertainOperations.ContainsKey(accountId);

    /// <summary>
    /// Commits an asset operation with the player's other changes. When the commit fails or its answer is lost, the
    /// outcome is read with a context of its own; while the database can't tell, it is asked again for a while, and then
    /// the account's asset operations wait (<see cref="IsUncertain"/>).
    /// </summary>
    /// <param name="player">The player; the caller holds its persistence lock.</param>
    /// <param name="request">The operation.</param>
    /// <param name="before">A hook right before the commit (tests), or <c>null</c>.</param>
    /// <param name="after">A hook right after the commit (tests), or <c>null</c>.</param>
    /// <returns>The outcome; <c>null</c> when it couldn't be read (the caller leaves everything as it is).</returns>
    public static async ValueTask<AssetCommitStatus?> CommitAsync(
        Player player,
        AssetOperationRequest request,
        Func<AssetOperationRequest, ValueTask>? before = null,
        Func<AssetOperationRequest, ValueTask>? after = null)
    {
        if (before is not null)
        {
            await before(request).ConfigureAwait(false);
        }

        try
        {
            var status = await player.PersistenceContext.SaveChangesWithAssetOperationAsync(request).ConfigureAwait(false);
            if (after is not null)
            {
                await after(request).ConfigureAwait(false);
            }

            return status;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            player.Logger.LogWarning(ex, "Asset operation {Operation} of {Player}: the commit failed or lost its answer; reading the outcome.", request.OperationId, player.Name);
        }

        for (var attempt = 0; attempt < OutcomeReadAttempts; attempt++)
        {
            try
            {
                using var check = player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration);
                if (await check.GetAssetOperationAsync(request.OperationId).ConfigureAwait(false) is not { } receipt)
                {
                    return AssetCommitStatus.Refused;
                }

                if (receipt.PayloadHash != request.PayloadHash)
                {
                    return AssetCommitStatus.Conflict;
                }

                // It went through: the changes are in the database, so they must not be written again.
                player.PersistenceContext.AcceptChangesOfCommittedOperation();
                return AssetCommitStatus.Committed;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                player.Logger.LogWarning(ex, "Asset operation {Operation}: the outcome couldn't be read (attempt {Attempt}).", request.OperationId, attempt + 1);
                await Task.Delay(OutcomeReadDelay * (attempt + 1)).ConfigureAwait(false);
            }
        }

        UncertainOperations[request.AccountId] = request.OperationId;
        player.Logger.LogError("Asset operation {Operation} of {Player}: the outcome is unknown; the account's asset operations wait until it is known.", request.OperationId, player.Name);
        return null;
    }
}
