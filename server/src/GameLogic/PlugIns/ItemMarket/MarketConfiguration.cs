// <copyright file="MarketConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ItemMarket;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Supported Item Market payment currencies.
/// </summary>
public enum MarketCurrency
{
    /// <summary>In-game Zen.</summary>
    Zen = 0,

    /// <summary>Account W Coin (C).</summary>
    WCoinC = 1,

    /// <summary>Account W Coin (P).</summary>
    WCoinP = 2,

    /// <summary>Account Goblin Points.</summary>
    GoblinPoints = 3,
}

/// <summary>
/// Configuration for <see cref="MarketPlugIn"/> (OFF by default).
/// </summary>
public sealed class MarketConfiguration
{
    /// <summary>Gets or sets the shared HNMO state file name.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets a value indicating whether buyers and sellers may browse/buy/cancel on the website.</summary>
    public bool AllowWebsite { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether Zen listings are permitted.</summary>
    public bool AllowZen { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether WCoinC listings are permitted.</summary>
    public bool AllowWCoinC { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether WCoinP listings are permitted.</summary>
    public bool AllowWCoinP { get; set; }

    /// <summary>Gets or sets a value indicating whether Goblin Points listings are permitted.</summary>
    public bool AllowGoblinPoints { get; set; } = true;

    /// <summary>Gets or sets the percentage fee charged on completed sales (deducted from seller payout).</summary>
    [Range(0, 50)]
    public double FeePercent { get; set; } = 5.0;

    /// <summary>Gets or sets the flat Zen fee charged when creating a listing.</summary>
    [Range(0, 100000000)]
    public int ListingZenFee { get; set; } = 10_000;

    /// <summary>Gets or sets the maximum number of simultaneous active listings per account.</summary>
    [Range(1, 50)]
    public int MaxListingsPerAccount { get; set; } = 10;

    /// <summary>Gets or sets how many hours an unsold listing stays active before expiring back to seller escrow.</summary>
    [Range(1, 720)]
    public int ExpiryHours { get; set; } = 168;

    /// <summary>Gets or sets the NPC number that opens the Item Market when talked to (default 547 Market Union Member Julia).</summary>
    [Range(0, 10000)]
    public short MarketNpcNumber { get; set; } = 547;

    /// <summary>Gets or sets a value indicating whether listing/buying/claiming requires standing in a safezone.</summary>
    public bool SafeZoneOnly { get; set; } = true;

    /// <summary>Gets or sets banned item keys in <c>group:number</c> form (e.g. <c>13:37</c>).</summary>
    public List<string> BannedItems { get; set; } = [];

    /// <summary>Gets or sets maximum audit entries retained in <c>save/hnmo-state.json</c>.</summary>
    [Range(10, 5000)]
    public int MaxAuditEntries { get; set; } = 500;
}
