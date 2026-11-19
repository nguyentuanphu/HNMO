// <copyright file="WorldBossesConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The weekly rankings.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WeeklyCategory>))]
public enum WeeklyCategory
{
    /// <summary>The boss-hunt points.</summary>
    BossHunt,

    /// <summary>The Blood Castle points.</summary>
    BloodCastle,

    /// <summary>The Devil Square points.</summary>
    DevilSquare,

    /// <summary>The Chaos Castle points.</summary>
    ChaosCastle,

    /// <summary>The castle siege winners.</summary>
    CastleSiege,
}

/// <summary>
/// How the spawn times of a world boss are set.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorldBossScheduleKind>))]
public enum WorldBossScheduleKind
{
    /// <summary>Every N hours of each allowed day, from the start time (e.g. 00:00, 04:00, 08:00, ...).</summary>
    EveryHours,

    /// <summary>At fixed times of each allowed day (e.g. 12:00 and 20:30).</summary>
    FixedTimes,
}

/// <summary>
/// What the damage ranking of a world boss counts.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorldBossRankBy>))]
public enum WorldBossRankBy
{
    /// <summary>Each player's own damage.</summary>
    Player,

    /// <summary>The damage of each party together (a player without a party counts alone); every member who did damage gets the party's rank.</summary>
    Party,
}

/// <summary>
/// The configuration of the <see cref="WorldBossesPlugIn"/>: the world bosses, the boss-hunt points and the weekly
/// ranking rewards.
/// </summary>
public class WorldBossesConfiguration
{
    /// <summary>Gets or sets the time zone of the schedules and of the weeks (an IANA name).</summary>
    [StringLength(64)]
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets a value indicating whether the announcements are shown in the game (golden notice).</summary>
    public bool AnnounceInGame { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the announcements are posted on Discord (when the Discord bridge is set up).</summary>
    public bool AnnounceOnDiscord { get; set; } = true;

    /// <summary>Gets or sets the world bosses.</summary>
    public ICollection<WorldBossDefinition> Bosses { get; set; } = CreateDefaultBosses();

    /// <summary>Gets or sets the weekly boss-hunt ranking.</summary>
    public WeeklyRankingConfiguration BossHunt { get; set; } = new() { Ranks = WeeklyRankingConfiguration.DefaultRanks(300, 200, 120, 50) };

    /// <summary>Gets or sets the weekly Blood Castle ranking.</summary>
    public WeeklyRankingConfiguration BloodCastle { get; set; } = new();

    /// <summary>Gets or sets the weekly Devil Square ranking.</summary>
    public WeeklyRankingConfiguration DevilSquare { get; set; } = new();

    /// <summary>Gets or sets the weekly Chaos Castle ranking.</summary>
    public WeeklyRankingConfiguration ChaosCastle { get; set; } = new();

    /// <summary>Gets or sets the weekly coins of the castle siege winners.</summary>
    public CastleSiegeWeeklyConfiguration CastleSiege { get; set; } = new();

    /// <summary>
    /// Gets or sets the file which keeps the state (next spawns, kills, damage, rewards waiting for room in the bag,
    /// rankings), relative to the server's folder; empty keeps it in memory only.
    /// </summary>
    [StringLength(260)]
    public string FileName { get; set; } = "../save/world-bosses.json";

    /// <summary>Gets the ranking of a category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The ranking configuration; <c>null</c> for the castle siege.</returns>
    public WeeklyRankingConfiguration? Ranking(WeeklyCategory category) => category switch
    {
        WeeklyCategory.BossHunt => this.BossHunt,
        WeeklyCategory.BloodCastle => this.BloodCastle,
        WeeklyCategory.DevilSquare => this.DevilSquare,
        WeeklyCategory.ChaosCastle => this.ChaosCastle,
        _ => null,
    };

    /// <summary>Gets the enabled bosses with an id.</summary>
    /// <returns>The bosses.</returns>
    public IEnumerable<WorldBossDefinition> EnabledBosses() => this.Bosses.Where(b => b.Enabled && !string.IsNullOrWhiteSpace(b.Id));

    /// <summary>
    /// Creates the default bosses.
    /// </summary>
    /// <returns>The bosses.</returns>
    public static List<WorldBossDefinition> CreateDefaultBosses()
    {
        static MissionReward Reward(int zen, int coins, params MissionItemReward[] items) => new() { Zen = zen, Coins = coins, Items = items.ToList() };
        static MissionItemReward Jewel(short group, short number, int count = 1) => new() { Group = group, Number = number, Count = count };
        return
        [
            new()
            {
                Id = "ice-queen", NameEn = "Ice Queen", NameVi = "Nữ Hoàng Băng Giá", MonsterNumber = 25, MapNumber = 2,
                ScheduleKind = WorldBossScheduleKind.EveryHours, EveryHours = 4, StartTime = "02:00", HuntPoints = 10,
                Top1 = Reward(2_000_000, 30, Jewel(14, 13)), Top2 = Reward(1_500_000, 20), Top3 = Reward(1_000_000, 15),
                Top10 = Reward(500_000, 5), Participation = Reward(200_000, 2), LastHit = Reward(0, 10),
            },
            new()
            {
                Id = "red-dragon", NameEn = "Red Dragon", NameVi = "Rồng Đỏ", MonsterNumber = 44, MapNumber = 0,
                ScheduleKind = WorldBossScheduleKind.FixedTimes, FixedTimes = "12:00, 20:00", HuntPoints = 20,
                Top1 = Reward(5_000_000, 60, Jewel(14, 14)), Top2 = Reward(3_000_000, 40, Jewel(14, 13)), Top3 = Reward(2_000_000, 30),
                Top10 = Reward(1_000_000, 10), Participation = Reward(300_000, 3), LastHit = Reward(0, 20),
            },
            new()
            {
                Id = "dark-phoenix", NameEn = "Dark Phoenix", NameVi = "Phượng Hoàng Bóng Tối", MonsterNumber = 77, MapNumber = 10,
                ScheduleKind = WorldBossScheduleKind.FixedTimes, FixedTimes = "21:00", Days = [DayOfWeek.Saturday, DayOfWeek.Sunday],
                DespawnMinutes = 45, AnnounceMinutesBefore = 15, HuntPoints = 50, RankBy = WorldBossRankBy.Party,
                Top1 = Reward(10_000_000, 150, Jewel(14, 16)), Top2 = Reward(6_000_000, 100, Jewel(14, 14)), Top3 = Reward(4_000_000, 70, Jewel(14, 13)),
                Top10 = Reward(2_000_000, 25), Participation = Reward(500_000, 5), LastHit = Reward(0, 30),
            },
        ];
    }
}
