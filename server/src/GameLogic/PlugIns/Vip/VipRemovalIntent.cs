// <copyright file="VipRemovalIntent.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>A trusted host removal bound to one account and one exact set of owned entitlement grants.</summary>
/// <param name="AccountId">The persistent account.</param>
/// <param name="OperationId">The stable host retry identifier.</param>
/// <param name="LoginName">The canonical account login.</param>
/// <param name="Attribution">The authenticated host and required reason.</param>
/// <param name="RequestedAtUtc">The server's original UTC timestamp.</param>
/// <param name="GrantIds">Only the three grants proved to belong to this VIP interval.</param>
public sealed record VipRemovalIntent(Guid AccountId, Guid OperationId, string LoginName, VipAdministrativeGrant Attribution, DateTime RequestedAtUtc, List<Guid> GrantIds);
