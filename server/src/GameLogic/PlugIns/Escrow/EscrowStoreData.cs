// <copyright file="EscrowStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System;
using System.Collections.Generic;

/// <summary>
/// Serialized root state of <see cref="EscrowStore"/> (<c>escrow</c> section).
/// </summary>
public sealed class EscrowStoreData
{
    /// <summary>Gets or sets all escrow contracts (open and historical).</summary>
    public List<EscrowContract> Contracts { get; set; } = [];

    /// <summary>Gets or sets pending owed item/Zen/Jewel deliveries.</summary>
    public List<EscrowOwedDelivery> OwedDeliveries { get; set; } = [];

    /// <summary>Gets or sets per-account trade reputation records keyed by login.</summary>
    public Dictionary<string, EscrowReputationEntry> Reputations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets active post-escrow item trade cooldown locks.</summary>
    public List<EscrowTradeCooldownEntry> ItemTradeCooldowns { get; set; } = [];

    /// <summary>Gets or sets collected host fee records (newest first).</summary>
    public List<EscrowFeeLedgerEntry> FeeLedger { get; set; } = [];

    /// <summary>Gets or sets consumed signed webhook nonces for replay protection.</summary>
    public List<EscrowWebhookNonceEntry> UsedWebhookNonces { get; set; } = [];
}
