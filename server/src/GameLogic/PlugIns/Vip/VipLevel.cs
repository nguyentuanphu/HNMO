// <copyright file="VipLevel.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.DataModel.Configuration;

/// <summary>A bilingual VIP level and the benefits supported by the initial rules model.</summary>
public sealed class VipLevel
{
    /// <summary>Gets or sets the stable, positive level identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the English display name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese display name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the additional experience percentage.</summary>
    public int ExperiencePercent { get; set; }

    /// <summary>Gets or sets the additional dropped-zen percentage.</summary>
    public int ZenPercent { get; set; }

    /// <summary>Gets or sets the daily coin bonus amount.</summary>
    public int DailyCoins { get; set; }

    /// <summary>Gets or sets the daily bonus currency.</summary>
    public CashShopCoinType DailyCoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the duration offers available for this level.</summary>
    public List<VipOffer> Offers { get; set; } = [];
}
