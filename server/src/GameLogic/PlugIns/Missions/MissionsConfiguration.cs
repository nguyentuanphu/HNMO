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
