// <copyright file="MarketPendingDelivery.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.IO;
using System.Text.Json;

/// <summary>
/// An item awaiting delivery to an account's in-game bag (bought item, canceled listing, or expired listing).
/// </summary>
public sealed class MarketPendingDelivery
{
    /// <summary>Gets or sets the delivery identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the recipient account login name.</summary>
    public string RecipientLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the item snapshot.</summary>
    public MarketItemSnapshot Item { get; set; } = new();

    /// <summary>Gets or sets origin reason (<c>Purchased</c>, <c>Canceled</c>, <c>Expired</c>).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; }
}
