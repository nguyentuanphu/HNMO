// <copyright file="MarketListing.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.IO;
using System.Text.Json;

/// <summary>
/// An active escrow listing on the Item Market.
/// </summary>
public sealed class MarketListing
{
    /// <summary>Gets or sets the unique listing ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets seller account login name.</summary>
    public string SellerLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets seller character name.</summary>
    public string SellerCharacter { get; set; } = string.Empty;

    /// <summary>Gets or sets the escrowed item snapshot.</summary>
    public MarketItemSnapshot Item { get; set; } = new();

    /// <summary>Gets or sets listing currency.</summary>
    public MarketCurrency Currency { get; set; }

    /// <summary>Gets or sets asking price.</summary>
    public int Price { get; set; }

    /// <summary>Gets or sets listing creation UTC timestamp.</summary>
    public DateTime ListedAtUtc { get; set; }

    /// <summary>Gets or sets listing expiry UTC timestamp.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}
