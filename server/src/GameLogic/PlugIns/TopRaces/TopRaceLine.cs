// <copyright file="TopRaceLine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A line of a finished race's table.
/// </summary>
public sealed class TopRaceLine
{
    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the entry's key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name (not shown).</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the class; -1 for a guild.</summary>
    public int Class { get; set; } = -1;

    /// <summary>Gets or sets the value.</summary>
    public long Value { get; set; }

    /// <summary>Gets or sets the coins of the prize.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; }

    /// <summary>Gets or sets the zen of the prize.</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets the items of the prize.</summary>
    public List<MissionItemReward> Items { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the line got a prize.</summary>
    public bool Prized { get; set; }
}
