// <copyright file="MarketPendingZen.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.IO;
using System.Text.Json;

/// <summary>
/// Net Zen payout owed to an offline or bag-locked seller.
/// </summary>
public sealed class MarketPendingZen
{
    /// <summary>Gets or sets the payout identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the seller account login name.</summary>
    public string SellerLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets net Zen amount after fee.</summary>
    public int NetZen { get; set; }

    /// <summary>Gets or sets associated listing ID.</summary>
    public string ListingId { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; }
}
