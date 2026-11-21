// <copyright file="WeeklyWinner.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A winner of a weekly ranking.
/// </summary>
public sealed class WeeklyWinner
{
    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the character id.</summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>Gets or sets the character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account (not shown on the website).</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the points.</summary>
    public long Points { get; set; }

    /// <summary>Gets or sets the coins it paid.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; }
}
