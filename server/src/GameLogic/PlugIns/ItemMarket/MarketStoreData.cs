// <copyright file="MarketStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.IO;
using System.Text.Json;

/// <summary>
/// Root store document saved under key <c>itemMarket</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class MarketStoreData
{
    /// <summary>Gets or sets active escrowed listings.</summary>
    public List<MarketListing> Listings { get; set; } = [];

    /// <summary>Gets or sets items awaiting in-game bag delivery.</summary>
    public List<MarketPendingDelivery> PendingDeliveries { get; set; } = [];

    /// <summary>Gets or sets net Zen payouts awaiting collection.</summary>
    public List<MarketPendingZen> PendingZen { get; set; } = [];

    /// <summary>Gets or sets the accounts (login names) which paid the seller access fee (they may buy too).</summary>
    public List<string> AccessPaid { get; set; } = [];

    /// <summary>Gets or sets the accounts (login names) which paid the buyer access fee.</summary>
    public List<string> BuyerAccessPaid { get; set; } = [];

    /// <summary>Gets or sets newest-first audit log entries.</summary>
    public List<MarketAuditEntry> AuditLog { get; set; } = [];
}
