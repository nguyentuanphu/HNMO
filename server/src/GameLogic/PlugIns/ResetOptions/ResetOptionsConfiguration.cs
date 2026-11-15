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
/// An item a reset or relife takes from the bag.
/// </summary>
public class ResetItemCost
{
    /// <summary>Gets or sets the item group.</summary>
    [Range(0, 15)]
    public short Group { get; set; } = 14;

    /// <summary>Gets or sets the item number in its group.</summary>
    [Range(0, 511)]
    public short Number { get; set; } = 13;

    /// <summary>Gets or sets the lowest item level which counts.</summary>
    [Range(0, 15)]
    public byte MinimumLevel { get; set; }

    /// <summary>Gets or sets how many pieces are taken.</summary>
    [Range(1, 100)]
    public int Count { get; set; } = 1;
}

/// <summary>
/// The rules of the resets from one reset count to another.
/// </summary>
public class ResetTier
{
    /// <summary>Gets or sets the lowest reset count (before the reset) this tier is for.</summary>
    [Range(0, 100_000)]
    public int FromResets { get; set; }

    /// <summary>Gets or sets the highest reset count (before the reset) this tier is for; -1 is no end.</summary>
    [Range(-1, 100_000)]
    public int ToResets { get; set; } = -1;

    /// <summary>Gets or sets the level needed.</summary>
    [Range(1, 2000)]
    public int RequiredLevel { get; set; } = 400;

    /// <summary>Gets or sets the level after the reset.</summary>
    [Range(1, 2000)]
    public int LevelAfterReset { get; set; } = 1;

    /// <summary>Gets or sets the zen it costs.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; }

    /// <summary>Gets or sets a value indicating whether the zen is multiplied by the reset number (reset 5 costs 5 × zen).</summary>
    public bool ZenTimesResetNumber { get; set; }

    /// <summary>Gets or sets the coins it costs.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="Coins"/>.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the stat points of one reset of this tier.</summary>
    [Range(0, 100_000)]
    public int Points { get; set; } = 500;

    /// <summary>Gets or sets what the reset does with the free points.</summary>
    public ResetPointsMode PointsMode { get; set; } = ResetPointsMode.Total;

    /// <summary>Gets or sets a value indicating whether the stats go back to the class's start values.</summary>
    public bool ClearStats { get; set; } = true;

    /// <summary>Gets or sets the resets a character may do per day (Sydney day); 0 is no limit.</summary>
    [Range(0, 1000)]
    public int DailyLimit { get; set; }

    /// <summary>Gets or sets the items it takes (none by default).</summary>
    public List<ResetItemCost> Items { get; set; } = new();

    /// <summary>
    /// Whether the tier is for a reset count (before the reset).
    /// </summary>
    /// <param name="resets">The reset count.</param>
    /// <returns><c>true</c>, if it is.</returns>
    public bool Covers(int resets) => resets >= this.FromResets && (this.ToResets < 0 || resets <= this.ToResets);
}

/// <summary>
/// The advantages of VIP accounts (the account's "Is VIP" value, from its attributes or a grant until a date).
/// </summary>
public class ResetVipOptions
{
    /// <summary>Gets or sets a value indicating whether VIP accounts get the advantages.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the lowest VIP value which counts.</summary>
    [Range(1, 100)]
    public int MinimumVipLevel { get; set; } = 1;

    /// <summary>Gets or sets how many levels lower a VIP resets.</summary>
    [Range(0, 1000)]
    public int LevelReduction { get; set; } = 20;

    /// <summary>Gets or sets the discount on the zen in percent.</summary>
    [Range(0, 100)]
    public int ZenDiscountPercent { get; set; } = 20;

    /// <summary>Gets or sets the discount on the coins in percent.</summary>
    [Range(0, 100)]
    public int CoinsDiscountPercent { get; set; } = 20;

    /// <summary>Gets or sets the extra resets per day (only where the tier has a limit).</summary>
    [Range(0, 1000)]
    public int ExtraDailyResets { get; set; } = 2;

    /// <summary>Gets or sets the extra stat points in percent.</summary>
    [Range(0, 1000)]
    public int ExtraPointsPercent { get; set; }

    /// <summary>Gets or sets a value indicating whether the relife gets the level reduction and the discounts too.</summary>
    public bool AppliesToRelife { get; set; } = true;
}

/// <summary>
/// The relife (grand reset): after enough resets, the resets go back to 0 (or keep a share) for permanent bonus points.
/// </summary>
public class RelifeOptions
{
    /// <summary>Gets or sets a value indicating whether relifes can be done.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the resets needed.</summary>
    [Range(1, 100_000)]
    public int ResetsNeeded { get; set; } = 50;

    /// <summary>Gets or sets the level needed.</summary>
    [Range(1, 2000)]
    public int RequiredLevel { get; set; } = 400;

    /// <summary>Gets or sets the level after the relife.</summary>
    [Range(1, 2000)]
    public int LevelAfterRelife { get; set; } = 1;

    /// <summary>Gets or sets the zen it costs.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; } = 50_000_000;

    /// <summary>Gets or sets the coins it costs.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="Coins"/>.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the share of the resets kept in percent (0: back to 0).</summary>
    [Range(0, 100)]
    public int KeepResetsPercent { get; set; }

    /// <summary>
    /// Gets or sets the permanent bonus points of each relife: they are part of every later point total (a character
    /// with 3 relifes has 3 × this on top of its resets' points).
    /// </summary>
    [Range(0, 1_000_000)]
    public int PointsPerRelife { get; set; } = 2000;

    /// <summary>Gets or sets the most relifes of a character; 0 is no limit.</summary>
    [Range(0, 10_000)]
    public int MaximumRelifes { get; set; } = 10;

    /// <summary>Gets or sets the relifes a character may do per day; 0 is no limit.</summary>
    [Range(0, 100)]
    public int DailyLimit { get; set; } = 1;

    /// <summary>Gets or sets the items it takes.</summary>
    public List<ResetItemCost> Items { get; set; } = new();
}

/// <summary>
/// The delegated reset ("ủy thác reset"): while a character is offline leveling, the server resets it when it reaches
/// the level needed, within the daily limits and paying the costs, and it goes on leveling.
/// </summary>
public class DelegatedResetOptions
{
    /// <summary>Gets or sets a value indicating whether players can switch it on.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the zen of each delegated reset on top of the tier's costs.</summary>
    [Range(0, 2_000_000_000)]
    public int ExtraZen { get; set; }

    /// <summary>Gets or sets the coins of each delegated reset on top of the tier's costs.</summary>
    [Range(0, 1_000_000)]
    public int ExtraCoins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="ExtraCoins"/>.</summary>
    public CashShopCoinType ExtraCoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the delegated resets per character and day; 0 is only the tier's limit.</summary>
    [Range(0, 1000)]
    public int DailyLimit { get; set; }

    /// <summary>Gets or sets a value indicating whether a reset which clears the stats puts the points back in the same shares (the character keeps its build and goes on hunting).</summary>
    public bool KeepBuild { get; set; } = true;

    /// <summary>Gets or sets how often the server looks at the offline characters, in seconds.</summary>
    [Range(5, 3600)]
    public int CheckSeconds { get; set; } = 30;
}

/// <summary>
/// What the website's account page may do.
/// </summary>
public class WebsiteResetOptions
{
    /// <summary>Gets or sets a value indicating whether characters can be reset on the website (only while the account is offline).</summary>
    public bool AllowReset { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether relifes can be done on the website.</summary>
    public bool AllowRelife { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether free points can be added to the stats on the website.</summary>
    public bool AllowAddStats { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the stats can be cleared (the points come back) on the website.</summary>
    public bool AllowClearStats { get; set; } = true;

    /// <summary>Gets or sets the zen clearing the stats costs.</summary>
    [Range(0, 2_000_000_000)]
    public int ClearStatsZen { get; set; } = 1_000_000;

    /// <summary>Gets or sets the coins clearing the stats costs.</summary>
    [Range(0, 1_000_000)]
    public int ClearStatsCoins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="ClearStatsCoins"/>.</summary>
    public CashShopCoinType ClearStatsCoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets a value indicating whether the delegated reset can be switched on and off on the website.</summary>
    public bool AllowDelegatedSwitch { get; set; } = true;
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
