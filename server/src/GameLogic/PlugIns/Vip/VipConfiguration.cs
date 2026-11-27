// <copyright file="VipConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.DataModel.Configuration;

/// <summary>Opt-in VIP rules. New purchases stay disabled until explicitly enabled by the host.</summary>
public sealed class VipConfiguration
{
    /// <summary>Gets or sets a value indicating whether VIP rules may authorize new activity.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets a value indicating whether active VIP characters receive a mark in local game chat.</summary>
    public bool ShowChatBadge { get; set; } = true;

    /// <summary>Gets or sets the available bilingual levels and prices.</summary>
    public List<VipLevel> Levels { get; set; } =
    [
        CreateLevel(1, "Bronze", "Đồng", 5, 10, 100),
        CreateLevel(2, "Silver", "Bạc", 10, 20, 200),
        CreateLevel(3, "Gold", "Vàng", 15, 30, 300),
    ];

    private static VipLevel CreateLevel(int id, string english, string vietnamese, int bonus, int dailyCoins, int weeklyPrice) => new()
    {
        Id = id,
        NameEn = english,
        NameVi = vietnamese,
        ExperiencePercent = bonus,
        ZenPercent = bonus,
        DailyCoins = dailyCoins,
        Offers =
        [
            new(7, weeklyPrice, CashShopCoinType.WCoinC),
            new(30, weeklyPrice * 4, CashShopCoinType.WCoinC),
            new(90, weeklyPrice * 11, CashShopCoinType.WCoinC),
        ],
    };
}
