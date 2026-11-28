// <copyright file="AssetOperationRequest.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Persistence;

using System.Threading;

/// <summary>
/// One operation on an account's assets, committed with its receipt (<see cref="DataModel.Entities.AssetOperation"/>)
/// and the context's other changes (the bag) in one database transaction.
/// </summary>
/// <param name="OperationId">The operation's identifier, the same for every attempt of this intent.</param>
/// <param name="AccountId">The account.</param>
/// <param name="Kind">The kind, e.g. <c>jewel-deposit</c>.</param>
/// <param name="PayloadHash">A hash of what the operation does; another payload under the same identifier is a conflict.</param>
/// <param name="Source">The source (<c>game</c>, <c>website</c>, <c>admin</c>, <c>import</c>).</param>
/// <param name="Detail">The details and outcome as JSON, or <c>null</c>.</param>
/// <param name="JewelDeltas">The Jewel Bank balance changes.</param>
/// <param name="PendingKey">What the operation leaves to deliver (a jewel key), or <c>null</c>.</param>
/// <param name="PendingCount">How many are left to deliver.</param>
/// <param name="DeliveredOperationId">An earlier operation whose pending delivery this one takes from, or <c>null</c>.</param>
/// <param name="DeliveredCount">How many of that delivery this one takes; refused if fewer are pending.</param>
/// <param name="FeatureState">Optional per-feature row state upserted in the same database transaction, or <c>null</c>.</param>
public sealed record AssetOperationRequest(
    Guid OperationId,
    Guid AccountId,
    string Kind,
    string PayloadHash,
    string Source,
    string? Detail,
    IReadOnlyList<JewelBankDelta> JewelDeltas,
    string? PendingKey = null,
    int PendingCount = 0,
    Guid? DeliveredOperationId = null,
    int DeliveredCount = 0,
    FeatureStateWrite? FeatureState = null);

/// <summary>
/// A feature state row write committed atomically alongside an <see cref="AssetOperationRequest"/>.
/// </summary>
/// <param name="Feature">The feature key (e.g. <c>s9.muun</c>).</param>
/// <param name="ScopeId">The scope identifier (e.g. character id).</param>
/// <param name="PayloadJson">The serialized JSON payload.</param>
/// <param name="UpdatedAtUtc">The UTC timestamp of the update.</param>
/// <param name="StateFileKey">Optional state-file key for in-memory/test backends.</param>
/// <param name="ExpiresAtUtc">Optional UTC expiration timestamp.</param>
public sealed record FeatureStateWrite(
    string Feature,
    string ScopeId,
    string PayloadJson,
    DateTime UpdatedAtUtc,
    string? StateFileKey = null,
    DateTime? ExpiresAtUtc = null);

/// <summary>
/// Relative Zen balance adjustment for one inventory storage in an atomic trade.
/// </summary>
/// <param name="StorageId">The <c>data."ItemStorage"</c> identifier.</param>
/// <param name="Delta">Signed Zen delta (<c>partnerOffer - selfOffer</c>).</param>
public sealed record TradeMoneyDelta(Guid StorageId, int Delta);

/// <summary>
/// Trade persistence context capable of committing item ownership, Zen transfers, and an asset operation receipt
/// inside a single database transaction.
/// </summary>
public interface IAtomicTradeContext : IContext
{
    /// <summary>
    /// Saves all tracked trade item changes together with the relative Zen adjustments and idempotent asset operation
    /// receipt in one database transaction.
    /// </summary>
    /// <param name="request">The trade operation receipt descriptor.</param>
    /// <param name="moneyChanges">Relative Zen changes for the participating inventory storages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The commit status.</returns>
    ValueTask<AssetCommitStatus> SaveTradeWithAssetOperationAsync(
        AssetOperationRequest request,
        IReadOnlyList<TradeMoneyDelta> moneyChanges,
        CancellationToken cancellationToken = default);
}
