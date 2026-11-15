// <copyright file="MissionsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;

/// <summary>
/// What a mission counts.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MissionKind>))]
public enum MissionKind
{
    /// <summary>Monsters killed by the character, optionally of one monster number and/or on one map.</summary>
    KillMonsters,

    /// <summary>Events (Blood Castle, Devil Square, Chaos Castle) the character was in when they ended.</summary>
    FinishEvent,

    /// <summary>Events the character won.</summary>
    WinEvent,

    /// <summary>Successful mixes at the chaos machine.</summary>
    ChaosMachineMix,

    /// <summary>Zen picked up from the ground.</summary>
    PickUpZen,

    /// <summary>Levels gained.</summary>
    LevelUp,

    /// <summary>Minutes in the game.</summary>
    PlayMinutes,
}

/// <summary>
/// When a mission starts again.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MissionPeriod>))]
public enum MissionPeriod
{
    /// <summary>Every day at the reset hour.</summary>
    Daily,

    /// <summary>Every Monday at the reset hour.</summary>
    Weekly,
}

/// <summary>
/// The events a <see cref="MissionKind.FinishEvent"/> or <see cref="MissionKind.WinEvent"/> mission counts.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MissionEventType>))]
public enum MissionEventType
{
    /// <summary>Blood Castle, Devil Square and Chaos Castle.</summary>
    Any,

    /// <summary>Blood Castle only.</summary>
    BloodCastle,

    /// <summary>Devil Square only.</summary>
    DevilSquare,

    /// <summary>Chaos Castle only.</summary>
    ChaosCastle,
}

/// <summary>
/// An item of a reward.
/// </summary>
public class MissionItemReward
{
    /// <summary>Gets or sets the item group.</summary>
    [Range(0, 15)]
    public short Group { get; set; }

    /// <summary>Gets or sets the item number in its group.</summary>
    [Range(0, 511)]
    public short Number { get; set; }

    /// <summary>Gets or sets the item level.</summary>
    [Range(0, 15)]
    public byte Level { get; set; }

    /// <summary>Gets or sets how many of the item are given.</summary>
    [Range(1, 50)]
    public int Count { get; set; } = 1;
}

/// <summary>
/// A reward: zen, items and coins.
/// </summary>
public class MissionReward
{
    /// <summary>Gets or sets the zen.</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public ICollection<MissionItemReward> Items { get; set; } = new List<MissionItemReward>();

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type of <see cref="Coins"/>.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets a value indicating whether the reward gives nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => this.Zen <= 0 && this.Coins <= 0 && this.Items.All(i => i.Count <= 0);
}

/// <summary>
/// A daily or weekly mission.
/// </summary>
public class MissionDefinition
{
    /// <summary>Gets or sets the identifier; the progress is kept by it, so it shouldn't change.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the mission is offered.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets when the mission starts again.</summary>
    public MissionPeriod Period { get; set; }

    /// <summary>Gets or sets what the mission counts.</summary>
    public MissionKind Kind { get; set; }

    /// <summary>Gets or sets the count to reach (zen for <see cref="MissionKind.PickUpZen"/>, minutes for <see cref="MissionKind.PlayMinutes"/>).</summary>
    [Range(1, 2_000_000_000)]
    public int Target { get; set; } = 1;

    /// <summary>Gets or sets the monster number which counts for <see cref="MissionKind.KillMonsters"/>; 0 counts every monster.</summary>
    [Range(0, 10000)]
    public short MonsterNumber { get; set; }

    /// <summary>Gets or sets the map number where kills count; -1 counts every map.</summary>
    [Range(-1, 255)]
    public short MapNumber { get; set; } = -1;

    /// <summary>Gets or sets the events which count for the event missions.</summary>
    public MissionEventType EventType { get; set; }

    /// <summary>Gets or sets the English title.</summary>
    [StringLength(120)]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    [StringLength(120)]
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the reward.</summary>
    public MissionReward Reward { get; set; } = new();
}

/// <summary>
/// Coins per hour in the game.
/// </summary>
public class OnlineTimeCoinsConfiguration
{
    /// <summary>Gets or sets a value indicating whether the source gives coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the coins per full hour in the game.</summary>
    [Range(0, 100_000)]
    public int CoinsPerHour { get; set; } = 5;

    /// <summary>Gets or sets the most coins of this source per account and day.</summary>
    [Range(0, 1_000_000)]
    public int DailyCap { get; set; } = 40;

    /// <summary>Gets or sets a value indicating whether time with auto play running counts half.</summary>
    public bool AutoPlayCountsHalf { get; set; } = true;
}

/// <summary>
/// The coins of a level.
/// </summary>
public class LevelCoins
{
    /// <summary>Gets or sets the level.</summary>
    [Range(1, 1000)]
    public int Level { get; set; }

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }
}

/// <summary>
/// One-time coins per character at level milestones and at each reset.
/// </summary>
public class MilestoneCoinsConfiguration
{
    /// <summary>Gets or sets a value indicating whether the source gives coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the levels and their coins.</summary>
    public ICollection<LevelCoins> Levels { get; set; } = new List<LevelCoins>
    {
        new() { Level = 100, Coins = 20 },
        new() { Level = 200, Coins = 30 },
        new() { Level = 300, Coins = 50 },
        new() { Level = 400, Coins = 80 },
    };

    /// <summary>Gets or sets the coins for each reset.</summary>
    [Range(0, 1_000_000)]
    public int CoinsPerReset { get; set; } = 50;
}

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

/// <summary>
/// The coins of a day of the login calendar.
/// </summary>
public class DayCoins
{
    /// <summary>Gets or sets the day number (the n-th login day of the month).</summary>
    [Range(1, 31)]
    public int Day { get; set; }

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }
}

/// <summary>
/// The monthly login calendar: one claim per day.
/// </summary>
public class LoginCalendarConfiguration
{
    /// <summary>Gets or sets a value indicating whether the source gives coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the coins of a day which isn't in <see cref="Days"/>.</summary>
    [Range(0, 1_000_000)]
    public int DefaultCoins { get; set; } = 5;

    /// <summary>Gets or sets the days with other coins.</summary>
    public ICollection<DayCoins> Days { get; set; } = new List<DayCoins>
    {
        new() { Day = 7, Coins = 20 },
        new() { Day = 14, Coins = 30 },
        new() { Day = 28, Coins = 60 },
    };

    /// <summary>
    /// Gets the coins of a day number.
    /// </summary>
    /// <param name="day">The day number.</param>
    /// <returns>The coins.</returns>
    public int CoinsOf(int day) => this.Days.LastOrDefault(d => d.Day == day)?.Coins ?? this.DefaultCoins;
}

/// <summary>
/// The configuration of the <see cref="MissionsPlugIn"/>: the daily and weekly missions, the streak and the earned coins.
/// </summary>
public class MissionsConfiguration
{
    /// <summary>
    /// Gets or sets the time zone of the reset hour (an IANA name).
    /// </summary>
    [StringLength(64)]
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>
    /// Gets or sets the hour of the day when the daily missions start again (weekly ones on Monday at that hour).
    /// </summary>
    [Range(0, 23)]
    public int ResetHour { get; set; }

    /// <summary>
    /// Gets or sets the missions.
    /// </summary>
    public ICollection<MissionDefinition> Missions { get; set; } = CreateDefaultMissions();

    /// <summary>
    /// Gets or sets how many daily missions finish a day of the streak; 0 means all of them.
    /// </summary>
    [Range(0, 100)]
    public int StreakMissionsRequired { get; set; }

    /// <summary>
    /// Gets or sets the day of the streak which pays the bonus (every that many days).
    /// </summary>
    [Range(2, 60)]
    public int StreakBonusDay { get; set; } = 7;

    /// <summary>
    /// Gets or sets the streak bonus.
    /// </summary>
    public MissionReward StreakBonus { get; set; } = new()
    {
        Coins = 50,
        Items = new List<MissionItemReward> { new() { Group = 14, Number = 13, Count = 1 } },
    };

    /// <summary>Gets or sets the coins for the online time.</summary>
    public OnlineTimeCoinsConfiguration OnlineTime { get; set; } = new();

    /// <summary>Gets or sets the coins at level milestones and resets.</summary>
    public MilestoneCoinsConfiguration Milestones { get; set; } = new();

    /// <summary>Gets or sets the coins for invites.</summary>
    public InviteCoinsConfiguration Invite { get; set; } = new();

    /// <summary>Gets or sets the login calendar.</summary>
    public LoginCalendarConfiguration LoginCalendar { get; set; } = new();

    /// <summary>
    /// Gets or sets the most earned coins per account and day, over the online time, milestones, invites and the login calendar.
    /// One-time rewards which don't fit wait for the next day.
    /// </summary>
    [Range(0, 10_000_000)]
    public int EarnedDailyCap { get; set; } = 300;

    /// <summary>
    /// Gets or sets the file which keeps the progress, relative to the server's folder (by default in the save folder,
    /// next to the database, so an update of the game folder keeps it); empty keeps it in memory only.
    /// </summary>
    [StringLength(260)]
    public string FileName { get; set; } = "../save/missions.json";

    /// <summary>
    /// Gets the enabled missions of a period.
    /// </summary>
    /// <param name="period">The period.</param>
    /// <returns>The missions.</returns>
    public IEnumerable<MissionDefinition> Enabled(MissionPeriod period) => this.Missions.Where(m => m.Enabled && m.Period == period && !string.IsNullOrWhiteSpace(m.Id));

    /// <summary>
    /// Creates the default missions.
    /// </summary>
    /// <returns>The missions.</returns>
    public static List<MissionDefinition> CreateDefaultMissions()
    {
        static MissionItemReward Jewel(short group, short number, int count = 1) => new() { Group = group, Number = number, Count = count };
        return
        [
            new() { Id = "daily-kill", Period = MissionPeriod.Daily, Kind = MissionKind.KillMonsters, Target = 300, TitleEn = "Defeat 300 monsters", TitleVi = "Hạ gục 300 quái vật", Reward = new() { Zen = 500_000, Coins = 10 } },
            new() { Id = "daily-event", Period = MissionPeriod.Daily, Kind = MissionKind.FinishEvent, Target = 1, TitleEn = "Take part in an event to the end (Blood Castle, Devil Square or Chaos Castle)", TitleVi = "Tham gia một sự kiện đến cuối (Huyết Lâu, Quảng Trường Quỷ hoặc Hỗn Nguyên Lâu)", Reward = new() { Items = [Jewel(14, 13)], Coins = 10 } },
            new() { Id = "daily-mix", Period = MissionPeriod.Daily, Kind = MissionKind.ChaosMachineMix, Target = 3, TitleEn = "Make 3 successful mixes at the chaos machine", TitleVi = "Pha chế thành công 3 lần ở máy Chaos", Reward = new() { Zen = 300_000, Coins = 5 } },
            new() { Id = "daily-zen", Period = MissionPeriod.Daily, Kind = MissionKind.PickUpZen, Target = 1_000_000, TitleEn = "Pick up 1,000,000 zen", TitleVi = "Nhặt 1.000.000 Zen", Reward = new() { Coins = 5 } },
            new() { Id = "daily-online", Period = MissionPeriod.Daily, Kind = MissionKind.PlayMinutes, Target = 60, TitleEn = "Play for 60 minutes", TitleVi = "Chơi 60 phút", Reward = new() { Zen = 200_000, Coins = 5 } },
            new() { Id = "weekly-kill", Period = MissionPeriod.Weekly, Kind = MissionKind.KillMonsters, Target = 3000, TitleEn = "Defeat 3,000 monsters", TitleVi = "Hạ gục 3.000 quái vật", Reward = new() { Items = [Jewel(14, 14, 2)], Coins = 40 } },
            new() { Id = "weekly-win", Period = MissionPeriod.Weekly, Kind = MissionKind.WinEvent, Target = 1, TitleEn = "Win an event", TitleVi = "Chiến thắng một sự kiện", Reward = new() { Items = [Jewel(12, 15)], Coins = 40 } },
            new() { Id = "weekly-mix", Period = MissionPeriod.Weekly, Kind = MissionKind.ChaosMachineMix, Target = 15, TitleEn = "Make 15 successful mixes at the chaos machine", TitleVi = "Pha chế thành công 15 lần ở máy Chaos", Reward = new() { Items = [Jewel(14, 16)], Coins = 30 } },
            new() { Id = "weekly-level", Period = MissionPeriod.Weekly, Kind = MissionKind.LevelUp, Target = 30, TitleEn = "Gain 30 levels", TitleVi = "Lên 30 cấp", Reward = new() { Zen = 2_000_000, Coins = 30 } },
        ];
    }
}
