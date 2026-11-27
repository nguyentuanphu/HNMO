// <copyright file="TopUpMilestoneStep.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using HNMO.DataModel.Configuration;

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
