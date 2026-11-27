// <copyright file="TopUpMilestonesConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using HNMO.DataModel.Configuration;

/// <summary>
/// Configuration for <see cref="TopUpMilestonesPlugIn"/>.
/// </summary>
public class TopUpMilestonesConfiguration
{
    /// <summary>The default state file path (resolved into <c>save/hnmo-state.json</c>).</summary>
    public const string DefaultFileName = "../save/topup-milestones.json";

    /// <summary>Gets or sets a value indicating whether the website account API/panel can show and claim milestones.</summary>
    public bool AllowWebsite { get; set; } = true;

    /// <summary>Gets or sets the wall clock time zone shown on history entries (default Sydney).</summary>
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets the state file name (keeps data across restarts via <see cref="HnmoStateStore"/>).</summary>
    public string FileName { get; set; } = DefaultFileName;

    /// <summary>Gets or sets the ordered milestone steps.</summary>
    public List<TopUpMilestoneStep> Steps { get; set; } =
    [
        new()
        {
            Id = "step-100k",
            ThresholdVnd = 100_000,
            TitleEn = "First Support (100k VND)",
            TitleVi = "Mốc ủng hộ 100k",
            CoinType = CashShopCoinType.WCoinC,
            Coins = 200,
            Items = [new() { Group = 14, Number = 13, Quantity = 2 }],
        },
        new()
        {
            Id = "step-500k",
            ThresholdVnd = 500_000,
            TitleEn = "Silver Patron (500k VND)",
            TitleVi = "Mốc ủng hộ 500k",
            CoinType = CashShopCoinType.WCoinC,
            Coins = 1_200,
            Items = [new() { Group = 14, Number = 14, Quantity = 5 }],
        },
        new()
        {
            Id = "step-1m",
            ThresholdVnd = 1_000_000,
            TitleEn = "Gold Patron (1M VND)",
            TitleVi = "Mốc ủng hộ 1 triệu",
            CoinType = CashShopCoinType.WCoinC,
            Coins = 3_000,
            Items = [new() { Group = 14, Number = 16, Quantity = 5 }],
        },
        new()
        {
            Id = "step-2m",
            ThresholdVnd = 2_000_000,
            TitleEn = "Platinum Patron (2M VND)",
            TitleVi = "Mốc ủng hộ 2 triệu",
            CoinType = CashShopCoinType.WCoinC,
            Coins = 7_500,
            Items = [new() { Group = 14, Number = 22, Quantity = 5 }],
        },
        new()
        {
            Id = "step-5m",
            ThresholdVnd = 5_000_000,
            TitleEn = "Legend Patron (5M VND)",
            TitleVi = "Mốc ủng hộ 5 triệu",
            CoinType = CashShopCoinType.WCoinC,
            Coins = 20_000,
            Items = [new() { Group = 14, Number = 14, Quantity = 15 }],
        },
    ];
}
