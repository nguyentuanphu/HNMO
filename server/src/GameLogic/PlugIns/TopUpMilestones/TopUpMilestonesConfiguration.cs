// <copyright file="TopUpMilestonesConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using HNMO.DataModel.Configuration;

/// <summary>
/// One item in a top-up milestone reward.
/// </summary>
public sealed class TopUpMilestoneItem
{
    /// <summary>Gets or sets the item group.</summary>
    public short Group { get; set; }

    /// <summary>Gets or sets the item number within the group.</summary>
    public short Number { get; set; }

    /// <summary>Gets or sets the item level (0-15).</summary>
    public byte Level { get; set; }

    /// <summary>Gets or sets the option level (0-7).</summary>
    public byte OptionLevel { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has Luck.</summary>
    public bool WithLuck { get; set; }

    /// <summary>Gets or sets a value indicating whether the item has Skill.</summary>
    public bool WithSkill { get; set; }

    /// <summary>Gets or sets the stack count or number of pieces.</summary>
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// A configurable top-up milestone step (VND threshold and one-time reward).
/// </summary>
public sealed class TopUpMilestoneStep
{
    /// <summary>Gets or sets the unique step identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the cumulative top-up threshold in VND.</summary>
    public long ThresholdVnd { get; set; } = 100_000;

    /// <summary>Gets or sets the English step title.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese step title.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets the coin amount granted.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the zen amount granted into the character's bag.</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets the items granted into the character's bag.</summary>
    public List<TopUpMilestoneItem> Items { get; set; } = [];
}

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
