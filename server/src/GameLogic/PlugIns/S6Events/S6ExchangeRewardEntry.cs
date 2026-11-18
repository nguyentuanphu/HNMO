// <copyright file="S6ExchangeRewardEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using HNMO.DataModel.Configuration;

/// <summary>
/// Weighted reward definition shared by Last Man Standing, Cherry Blossom, and Lucky Coin exchanges.
/// </summary>
public sealed class S6ExchangeRewardEntry
{
    /// <summary>Gets or sets selection weight (&gt; 0).</summary>
    public int Weight { get; set; } = 100;

    /// <summary>Gets or sets English reward name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese reward name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets Zen awarded.</summary>
    public uint Zen { get; set; }

    /// <summary>Gets or sets cash shop coin amount awarded via <see cref="Missions.MissionCoinGranter"/>.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets cash shop coin type awarded.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets item group when granting an item reward (-1 = none).</summary>
    public int ItemGroup { get; set; } = -1;

    /// <summary>Gets or sets item number when granting an item reward.</summary>
    public short ItemNumber { get; set; }

    /// <summary>Gets or sets item upgrade level.</summary>
    public byte ItemLevel { get; set; }

    /// <summary>Gets or sets stack/item count (at least 1 when <see cref="ItemGroup"/> &gt;= 0).</summary>
    public int ItemCount { get; set; } = 1;
}
