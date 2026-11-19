// <copyright file="ResetOptionsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using HNMO.DataModel.Configuration;

/// <summary>
/// What a reset does with the free stat points.
/// </summary>
public enum ResetPointsMode
{
    /// <summary>The free points become the points of every reset done so far (the tiers' points per reset), plus the relife bonus.</summary>
    Total,

    /// <summary>The reset's points are added to the free points.</summary>
    Add,

    /// <summary>No points are given; the free points stay as they are.</summary>
    Keep,
}

/// <summary>
/// Where a reset was done.
/// </summary>
public enum ResetSource
{
    /// <summary>In the game (the reset NPC, /reset, /relife).</summary>
    Game,

    /// <summary>On the website's account page.</summary>
    Website,

    /// <summary>By the server during offline leveling (delegated reset).</summary>
    Delegated,
}

/// <summary>
/// The configuration of HNMO's reset options: tiers, VIP, relife, delegated reset, the website and the monthly ranking.
/// While the plug-in is on, the reset NPC and /reset follow these rules; when it's off, the plain reset feature's rules.
/// </summary>
public class ResetOptionsConfiguration
{
    /// <summary>Gets or sets the time zone of the days and months.</summary>
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets the hour at which a new day starts (for the daily limits).</summary>
    [Range(0, 23)]
    public int DayStartHour { get; set; }

    /// <summary>Gets or sets the file of the state (relifes, daily counts, the monthly ranking, the log).</summary>
    public string FileName { get; set; } = "../save/resets.json";

    /// <summary>Gets or sets the most resets of a character; 0 is no limit.</summary>
    [Range(0, 100_000)]
    public int MaximumResets { get; set; }

    /// <summary>Gets or sets a value indicating whether a reset in the game moves the character to its home town.</summary>
    public bool MoveHome { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a reset in the game goes back to the character selection.</summary>
    public bool LogOut { get; set; }

    /// <summary>Gets or sets the tiers; the first tier which covers the reset count counts.</summary>
    public List<ResetTier> Tiers { get; set; } = DefaultTiers();

    /// <summary>Gets or sets the VIP options.</summary>
    public ResetVipOptions Vip { get; set; } = new();

    /// <summary>Gets or sets the relife options.</summary>
    public RelifeOptions Relife { get; set; } = new();

    /// <summary>Gets or sets the delegated reset options.</summary>
    public DelegatedResetOptions Delegated { get; set; } = new();

    /// <summary>Gets or sets the website options.</summary>
    public WebsiteResetOptions Website { get; set; } = new();

    /// <summary>Gets or sets how many characters the rankings show.</summary>
    [Range(5, 500)]
    public int RankingSize { get; set; } = 50;

    /// <summary>Gets or sets how many log lines are kept.</summary>
    [Range(10, 100_000)]
    public int LogKept { get; set; } = 5000;

    /// <summary>
    /// The default tiers: up to 49 resets, 50 to 99, 100 and more.
    /// </summary>
    /// <returns>The tiers.</returns>
    public static List<ResetTier> DefaultTiers() =>
    [
        new ResetTier { FromResets = 0, ToResets = 49, RequiredLevel = 350, LevelAfterReset = 1, Zen = 5_000_000, Points = 500, DailyLimit = 20 },
        new ResetTier { FromResets = 50, ToResets = 99, RequiredLevel = 380, LevelAfterReset = 1, Zen = 10_000_000, Points = 600, DailyLimit = 10 },
        new ResetTier { FromResets = 100, ToResets = -1, RequiredLevel = 400, LevelAfterReset = 1, Zen = 20_000_000, Coins = 10, Points = 700, DailyLimit = 5 },
    ];
}
