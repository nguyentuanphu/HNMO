// <copyright file="LeftoverExchangeOffer.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// Exchange NPC offer converting surplus event/boss materials into coins, Zen, or rewards.
/// </summary>
public sealed class LeftoverExchangeOffer
{
    /// <summary>Gets or sets unique offer ID.</summary>
    [StringLength(32)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets English offer title.</summary>
    [StringLength(80)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese offer title.</summary>
    [StringLength(80)]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this exchange is active.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets input item group.</summary>
    [Range(0, 15)]
    public byte InputGroup { get; set; }

    /// <summary>Gets or sets input item number.</summary>
    [Range(0, 511)]
    public short InputNumber { get; set; }

    /// <summary>Gets or sets exact level required (<c>-1</c> = any).</summary>
    [Range(-1, 15)]
    public int InputLevel { get; set; } = -1;

    /// <summary>Gets or sets number of surplus items required.</summary>
    [Range(1, 50)]
    public int InputCount { get; set; } = 5;

    /// <summary>Gets or sets Coin reward amount.</summary>
    [Range(0, 100_000)]
    public int RewardCoins { get; set; } = 50;

    /// <summary>Gets or sets coin currency.</summary>
    public CashShopCoinType RewardCoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets Zen reward amount.</summary>
    [Range(0, 1_000_000_000)]
    public int RewardZen { get; set; }

    /// <summary>Gets or sets optional reward item count (0 = item reward omitted).</summary>
    [Range(0, 20)]
    public int RewardItemCount { get; set; }

    /// <summary>Gets or sets optional reward item group.</summary>
    [Range(0, 15)]
    public byte RewardGroup { get; set; }

    /// <summary>Gets or sets optional reward item number.</summary>
    [Range(0, 511)]
    public short RewardNumber { get; set; }

    /// <summary>Gets or sets optional reward item level.</summary>
    [Range(0, 15)]
    public byte RewardLevel { get; set; }
}
