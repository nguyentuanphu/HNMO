// <copyright file="AssetOperationSummary.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

/// <summary>
/// A committed <see cref="DataModel.Entities.AssetOperation"/> receipt with its account login name for admin history views.
/// </summary>
/// <param name="Id">The operation identifier.</param>
/// <param name="AccountId">The account identifier.</param>
/// <param name="LoginName">The account login name, or the identifier when the account is missing.</param>
/// <param name="Kind">The operation kind.</param>
/// <param name="Detail">The operation detail text or JSON.</param>
/// <param name="PendingKey">The pending delivery key, if any.</param>
/// <param name="PendingCount">The remaining quantity to deliver.</param>
/// <param name="Source">The origin (<c>game</c>, <c>website</c>, <c>admin</c>, <c>import</c>).</param>
/// <param name="CreatedAt">The UTC timestamp when committed.</param>
public sealed record AssetOperationSummary(
    Guid Id,
    Guid AccountId,
    string LoginName,
    string Kind,
    string? Detail,
    string? PendingKey,
    int PendingCount,
    string Source,
    DateTime CreatedAt);
