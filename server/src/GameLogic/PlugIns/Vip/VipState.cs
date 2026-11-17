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
    /// <summary>Gets or sets durable purchase intents, keyed by retry identifier.</summary>
    public Dictionary<Guid, VipWalletIntent> WalletIntents { get; set; } = [];

    /// <summary>Gets or sets the account grant identifiers owned by the last confirmed VIP transaction.</summary>
    public Dictionary<string, List<Guid>> OwnedGrants { get; set; } = [];
}
