// <copyright file="LuckyNumberConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;

/// <summary>
/// Configuration for <see cref="LuckyNumberPlugIn"/> (OFF by default).
/// Players pick a daily number in <see cref="MinNumber"/>..<see cref="MaxNumber"/> before the daily draw (Sydney time);
/// exact matches (and optional last-digit matches) win coins and/or jewels.
/// </summary>
public sealed class LuckyNumberConfiguration
{
    /// <summary>Gets or sets the shared HNMO state file name.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets a value indicating whether players can pick numbers from the website portal.</summary>
    public bool AllowWebsite { get; set; } = true;

    /// <summary>Gets or sets the wall-clock schedule time zone identifier.</summary>
    [StringLength(64)]
    public string TimeZoneId { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets the daily draw time in the server time zone as <c>HH:mm</c>.</summary>
    [StringLength(5)]
    public string DrawTime { get; set; } = "20:00";

    /// <summary>Gets or sets the inclusive minimum pickable number.</summary>
    [Range(0, 9999)]
    public int MinNumber { get; set; }

    /// <summary>Gets or sets the inclusive maximum pickable number.</summary>
    [Range(1, 9999)]
    public int MaxNumber { get; set; } = 99;

    /// <summary>Gets or sets how many numbers one account can pick per daily draw.</summary>
    [Range(1, 20)]
    public int MaxPicksPerAccountPerDay { get; set; } = 1;

    /// <summary>Gets or sets optional zen cost per pick in-game (0 for free).</summary>
    [Range(0, 2000000000)]
    public int EntryCostZen { get; set; }

    /// <summary>Gets or sets exact-match winning coins.</summary>
    [Range(0, 1000000)]
    public int ExactMatchCoins { get; set; } = 100;

    /// <summary>Gets or sets the coin currency for exact matches.</summary>
    public CashShopCoinType ExactMatchCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets the jewel item group for exact matches (14 = Jewels).</summary>
    [Range(0, 15)]
    public int ExactMatchJewelGroup { get; set; } = 14;

    /// <summary>Gets or sets the jewel item number for exact matches (13 = Jewel of Bless, 14 = Soul, 16 = Life, 22 = Creation).</summary>
    [Range(0, 511)]
    public int ExactMatchJewelNumber { get; set; } = 13;

    /// <summary>Gets or sets the number of jewels granted to exact-match winners (0 for coins only).</summary>
    [Range(0, 50)]
    public int ExactMatchJewelCount { get; set; } = 2;

    /// <summary>Gets or sets a value indicating whether matching the last digit earns a consolation prize.</summary>
    public bool EnableLastDigitPrize { get; set; } = true;

    /// <summary>Gets or sets last-digit consolation winning coins.</summary>
    [Range(0, 1000000)]
    public int LastDigitCoins { get; set; } = 15;

    /// <summary>Gets or sets the coin currency for last-digit winners.</summary>
    public CashShopCoinType LastDigitCoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the jewel item group for last-digit winners.</summary>
    [Range(0, 15)]
    public int LastDigitJewelGroup { get; set; } = 14;

    /// <summary>Gets or sets the jewel item number for last-digit winners.</summary>
    [Range(0, 511)]
    public int LastDigitJewelNumber { get; set; } = 14;

    /// <summary>Gets or sets the number of jewels granted to last-digit winners.</summary>
    [Range(0, 50)]
    public int LastDigitJewelCount { get; set; }
}
