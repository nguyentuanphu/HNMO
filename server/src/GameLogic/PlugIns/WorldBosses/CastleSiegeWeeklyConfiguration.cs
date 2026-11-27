// <copyright file="CastleSiegeWeeklyConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The weekly coins of the members of the guild which won the castle siege that week.
/// </summary>
public class CastleSiegeWeeklyConfiguration
{
    /// <summary>Gets or sets a value indicating whether the winners get coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the coins of each member of the winning guild.</summary>
    [Range(0, 1_000_000)]
    public int CoinsPerMember { get; set; } = 100;

    /// <summary>Gets or sets the extra coins of the guild master.</summary>
    [Range(0, 1_000_000)]
    public int GuildMasterBonus { get; set; } = 100;
}
