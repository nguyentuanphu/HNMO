// <copyright file="VipState.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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

    /// <summary>Gets or sets terminal refusals for intents which never changed the account.</summary>
    public Dictionary<Guid, VipRuleResult> WalletRefusals { get; set; } = [];

    /// <summary>Gets or sets account-bound database receipt references for rebuilding the shared-state projection.</summary>
    public Dictionary<string, string> WalletReferences { get; set; } = [];

    /// <summary>Gets or sets host removal intents persisted before the account mutation.</summary>
    public Dictionary<Guid, VipRemovalIntent> RemovalIntents { get; set; } = [];

    /// <summary>Gets or sets confirmed host removal identifiers for projection and audit replay protection.</summary>
    public HashSet<Guid> RemovedOperations { get; set; } = [];
}
