// <copyright file="VipState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

/// <summary>The VIP section of the shared state document.</summary>
public sealed class VipState
{
    /// <summary>Gets or sets subscriptions by canonical login name.</summary>
    public Dictionary<string, VipMembership> Memberships { get; set; } = [];

    /// <summary>Gets or sets confirmed payment identifiers and their original quotes for replay protection.</summary>
    public Dictionary<Guid, VipPurchasePlan> Payments { get; set; } = [];

    /// <summary>Gets or sets the last claimed Sydney calendar day by account.</summary>
    public Dictionary<string, string> DailyClaims { get; set; } = [];

    /// <summary>Gets or sets subscription-change audit entries.</summary>
    public List<VipAuditEntry> Audit { get; set; } = [];
}
