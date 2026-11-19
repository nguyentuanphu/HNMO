// <copyright file="LuckyNumberWinnerEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.LuckyNumber;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;

/// <summary>
/// A winning pick from a completed daily draw.
/// </summary>
public sealed class LuckyNumberWinnerEntry
{
    /// <summary>Gets or sets the winning account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the character display name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the picked number.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the prize tier (<c>exact</c> or <c>lastDigit</c>).</summary>
    public string Tier { get; set; } = "exact";

    /// <summary>Gets or sets coins awarded.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets coin currency.</summary>
    public CashShopCoinType CoinType { get; set; }

    /// <summary>Gets or sets jewel count owed/granted.</summary>
    public int JewelCount { get; set; }

    /// <summary>Gets or sets the jewel item group.</summary>
    public int JewelGroup { get; set; }

    /// <summary>Gets or sets the jewel item number.</summary>
    public int JewelNumber { get; set; }

    /// <summary>Gets or sets a value indicating whether coins have been queued/granted.</summary>
    public bool CoinsGranted { get; set; }
}
