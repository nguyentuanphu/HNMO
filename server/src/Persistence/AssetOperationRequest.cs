// <copyright file="AssetOperationRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

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
    int DeliveredCount = 0);
