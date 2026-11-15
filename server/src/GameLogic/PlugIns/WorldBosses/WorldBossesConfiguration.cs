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
/// A world boss: a strong monster which appears on a schedule on a map, is announced in the game, on Discord and on
/// the website, and pays rewards by the damage dealt.
/// </summary>
public class WorldBossDefinition
{
    /// <summary>Gets or sets the identifier; the state is kept by it, so it shouldn't change.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the boss appears.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the English name.</summary>
    [StringLength(60)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese name.</summary>
    [StringLength(60)]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the monster number.</summary>
    [Range(0, 10000)]
    public short MonsterNumber { get; set; }

    /// <summary>Gets or sets the map number.</summary>
    [Range(0, 255)]
    public short MapNumber { get; set; }

    /// <summary>Gets or sets the game server which hosts the boss; -1 is the first game server (the lowest number) of this process.</summary>
    [Range(-1, 255)]
    public int GameServerId { get; set; } = -1;

    /// <summary>Gets or sets the spawn spots as "x,y; x,y"; one of them is picked. Empty picks a random walkable spot of the area.</summary>
    [StringLength(400)]
    public string SpawnSpots { get; set; } = string.Empty;

    /// <summary>Gets or sets the left edge of the spawn area (used without spawn spots).</summary>
    [Range(0, 255)]
    public byte AreaX1 { get; set; }

    /// <summary>Gets or sets the top edge of the spawn area.</summary>
    [Range(0, 255)]
    public byte AreaY1 { get; set; }

    /// <summary>Gets or sets the right edge of the spawn area.</summary>
    [Range(0, 255)]
    public byte AreaX2 { get; set; } = 255;

    /// <summary>Gets or sets the bottom edge of the spawn area.</summary>
    [Range(0, 255)]
    public byte AreaY2 { get; set; } = 255;

    /// <summary>Gets or sets how the spawn times are set.</summary>
    public WorldBossScheduleKind ScheduleKind { get; set; }

    /// <summary>Gets or sets the hours between two spawns of <see cref="WorldBossScheduleKind.EveryHours"/>.</summary>
    [Range(1, 24)]
    public int EveryHours { get; set; } = 4;

    /// <summary>Gets or sets the first time of the day of <see cref="WorldBossScheduleKind.EveryHours"/> as "HH:mm".</summary>
    [StringLength(5)]
    public string StartTime { get; set; } = "00:00";

    /// <summary>Gets or sets the times of <see cref="WorldBossScheduleKind.FixedTimes"/> as "HH:mm, HH:mm".</summary>
    [StringLength(200)]
    public string FixedTimes { get; set; } = "20:00";

    /// <summary>Gets or sets the days of the week when the boss appears; empty means every day.</summary>
    public ICollection<DayOfWeek> Days { get; set; } = new List<DayOfWeek>();

    /// <summary>Gets or sets how many minutes before the spawn it's announced (0: no announcement before).</summary>
    [Range(0, 240)]
    public int AnnounceMinutesBefore { get; set; } = 10;

    /// <summary>Gets or sets after how many minutes the boss leaves when it wasn't killed.</summary>
    [Range(1, 1440)]
    public int DespawnMinutes { get; set; } = 30;

    /// <summary>Gets or sets the English text announced before the spawn; {boss}, {map} and {minutes} are filled in.</summary>
    [StringLength(200)]
    public string SoonTextEn { get; set; } = "{boss} appears in {map} in {minutes} minutes!";

    /// <summary>Gets or sets the Vietnamese text announced before the spawn.</summary>
    [StringLength(200)]
    public string SoonTextVi { get; set; } = "{boss} sẽ xuất hiện ở {map} sau {minutes} phút!";

    /// <summary>Gets or sets the English text announced at the spawn; {boss}, {map}, {x}, {y} and {minutes} (until it leaves) are filled in.</summary>
    [StringLength(200)]
    public string SpawnTextEn { get; set; } = "{boss} has appeared in {map} ({x}, {y})! It leaves in {minutes} minutes.";

    /// <summary>Gets or sets the Vietnamese text announced at the spawn.</summary>
    [StringLength(200)]
    public string SpawnTextVi { get; set; } = "{boss} đã xuất hiện ở {map} ({x}, {y})! Boss rời đi sau {minutes} phút.";

    /// <summary>Gets or sets the English text announced at the kill; {boss}, {map}, {killer} and {top} (the best damage dealer) are filled in.</summary>
    [StringLength(200)]
    public string KillTextEn { get; set; } = "{boss} was defeated in {map}! Last hit: {killer}, most damage: {top}.";

    /// <summary>Gets or sets the Vietnamese text announced at the kill.</summary>
    [StringLength(200)]
    public string KillTextVi { get; set; } = "{boss} đã bị hạ gục ở {map}! Đòn cuối: {killer}, sát thương cao nhất: {top}.";

    /// <summary>Gets or sets the English text when the boss leaves without being killed.</summary>
    [StringLength(200)]
    public string EscapeTextEn { get; set; } = "{boss} has left {map}.";

    /// <summary>Gets or sets the Vietnamese text when the boss leaves.</summary>
    [StringLength(200)]
    public string EscapeTextVi { get; set; } = "{boss} đã rời khỏi {map}.";

    /// <summary>Gets or sets what the damage ranking counts.</summary>
    public WorldBossRankBy RankBy { get; set; }

    /// <summary>Gets or sets the reward of rank 1.</summary>
    public MissionReward Top1 { get; set; } = new();

    /// <summary>Gets or sets the reward of rank 2.</summary>
    public MissionReward Top2 { get; set; } = new();

    /// <summary>Gets or sets the reward of rank 3.</summary>
    public MissionReward Top3 { get; set; } = new();

    /// <summary>Gets or sets the reward of the ranks 4 to 10.</summary>
    public MissionReward Top10 { get; set; } = new();

    /// <summary>Gets or sets the reward of everyone else who did at least the minimum damage.</summary>
    public MissionReward Participation { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the top 10 get the participation reward too.</summary>
    public bool TopAlsoGetParticipation { get; set; }

    /// <summary>Gets or sets the share of all damage (in percent) which earns the participation reward.</summary>
    [Range(0, 100)]
    public double ParticipationMinimumPercent { get; set; } = 1;

    /// <summary>Gets or sets the damage which earns the participation reward, whatever the share (0: only the share counts).</summary>
    [Range(0, long.MaxValue)]
    public long ParticipationMinimumDamage { get; set; }

    /// <summary>Gets or sets the bonus of the player with the last hit.</summary>
    public MissionReward LastHit { get; set; } = new();

    /// <summary>
    /// Gets or sets the boss-hunt points of a kill, shared by the damage (each player who did damage gets at least 1).
    /// </summary>
    [Range(0, 100_000)]
    public int HuntPoints { get; set; } = 10;
}

/// <summary>
/// The coins of a rank.
/// </summary>
public class RankCoins
{
    /// <summary>Gets or sets the rank (1 to 10).</summary>
    [Range(1, 10)]
    public int Rank { get; set; }

    /// <summary>Gets or sets the coins.</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }
}

/// <summary>
/// A weekly ranking (Monday to Sunday) whose top 10 get coins when the week ends.
/// </summary>
public class WeeklyRankingConfiguration
{
    /// <summary>Gets or sets a value indicating whether the ranking pays coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the coins of the ranks.</summary>
    public ICollection<RankCoins> Ranks { get; set; } = DefaultRanks(200, 120, 80, 30);

    /// <summary>Gets or sets the points of a character who was in the event when it ended (events only).</summary>
    [Range(0, 10_000)]
    public int PointsFinish { get; set; } = 1;

    /// <summary>Gets or sets the points of the event's winner (events only).</summary>
    [Range(0, 10_000)]
    public int PointsWin { get; set; } = 3;

    /// <summary>Gets the coins of a rank.</summary>
    /// <param name="rank">The rank.</param>
    /// <returns>The coins.</returns>
    public int CoinsOf(int rank) => this.Ranks.LastOrDefault(r => r.Rank == rank)?.Coins ?? 0;

    /// <summary>
    /// Creates ranks 1, 2 and 3 with their coins and the ranks 4 to 10 with the last amount.
    /// </summary>
    /// <param name="first">Rank 1.</param>
    /// <param name="second">Rank 2.</param>
    /// <param name="third">Rank 3.</param>
    /// <param name="rest">Ranks 4 to 10.</param>
    /// <returns>The ranks.</returns>
    public static List<RankCoins> DefaultRanks(int first, int second, int third, int rest) =>
        Enumerable.Range(1, 10).Select(r => new RankCoins { Rank = r, Coins = r switch { 1 => first, 2 => second, 3 => third, _ => rest } }).ToList();
}

/// <summary>
/// The weekly coins of the members of the guild which won the castle siege that week.
/// </summary>
public class CastleSiegeWeeklyConfiguration
{
    /// <summary>Gets or sets a value indicating whether the winners get coins.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the coins of each member of the winning guild.</summary>
    [Range(0, 1_000_000)]
    public int CoinsPerMember { get; set; } = 100;

    /// <summary>Gets or sets the extra coins of the guild master.</summary>
    [Range(0, 1_000_000)]
    public int GuildMasterBonus { get; set; } = 100;
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
