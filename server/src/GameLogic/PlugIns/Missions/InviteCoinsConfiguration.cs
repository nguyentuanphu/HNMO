// <copyright file="InviteCoinsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// Coins for inviting a friend.
/// </summary>
public class InviteCoinsConfiguration
{
    /// <summary>Gets or sets a value indicating whether the source gives coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the level a character of the invited account has to reach.</summary>
    [Range(1, 1000)]
    public int RequiredLevel { get; set; } = 150;

    /// <summary>Gets or sets the coins of the inviting account.</summary>
    [Range(0, 1_000_000)]
    public int InviterCoins { get; set; } = 100;

    /// <summary>Gets or sets the coins of the invited account (0: none).</summary>
    [Range(0, 1_000_000)]
    public int InvitedCoins { get; set; } = 30;

    /// <summary>Gets or sets a value indicating whether an invite from the same network address is refused.</summary>
    public bool RefuseSameAddress { get; set; } = true;
}
