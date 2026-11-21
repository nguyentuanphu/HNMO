// <copyright file="OnboardingOwedReward.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Onboarding;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// A reward which waits to be given: its zen, items, stat points and buff need the character in the game with room in
/// the bag; its coins are given at once (to the account).
/// </summary>
public sealed class OnboardingOwedReward
{
    /// <summary>Gets or sets the reference; it makes the reward count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets what it's for: "step", "final" or "kit".</summary>
    public string Kind { get; set; } = "step";

    /// <summary>Gets or sets the step id, for a step.</summary>
    public string? StepId { get; set; }

    /// <summary>Gets or sets the English title (the step's or "Starter kit").</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the reason (for the coins history and the logs).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the zen.</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public List<StarterKitItem> Items { get; set; } = new();

    /// <summary>Gets or sets the coins.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets a value indicating whether the coins were given.</summary>
    public bool CoinsGiven { get; set; }

    /// <summary>Gets or sets the minutes of the EXP buff (0: none).</summary>
    public int BuffMinutes { get; set; }

    /// <summary>Gets or sets the item group whose effect is the buff.</summary>
    public short BuffItemGroup { get; set; }

    /// <summary>Gets or sets the item number whose effect is the buff.</summary>
    public short BuffItemNumber { get; set; }

    /// <summary>Gets or sets when it was earned.</summary>
    public DateTime EarnedAtUtc { get; set; }

    /// <summary>Gets a value indicating whether something needs the character in the game.</summary>
    [JsonIgnore]
    public bool HasGoods => this.Zen > 0 || this.BuffMinutes > 0 || this.Items.Any(i => i.Count > 0);
}
