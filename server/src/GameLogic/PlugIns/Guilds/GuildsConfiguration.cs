// <copyright file="GuildsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// What the members did that gives their guild experience.
/// </summary>
public enum GuildXpSource
{
    /// <summary>A monster defeated by a member.</summary>
    MonsterKill,

    /// <summary>A member was in Blood Castle, Devil Square or Chaos Castle at its end.</summary>
    EventFinish,

    /// <summary>A member won Blood Castle, Devil Square or Chaos Castle.</summary>
    EventWin,

    /// <summary>A member took part in a world boss kill (with damage).</summary>
    WorldBossKill,

    /// <summary>A member's share of a world boss's damage, per percent.</summary>
    WorldBossDamage,

    /// <summary>A member finished (claimed) a daily or weekly mission.</summary>
    Mission,

    /// <summary>A member's time in the game.</summary>
    OnlineTime,

    /// <summary>The guild won the castle siege (not counted against a member's daily cap).</summary>
    CastleSiege,
}

/// <summary>
/// The configuration of HNMO's guild levels, guild buffs and weekly guild ladder.
/// </summary>
public class GuildsConfiguration
{
    /// <summary>Gets or sets the time zone of the days (daily cap) and of the weeks (ladder), an IANA name.</summary>
    [StringLength(64)]
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets the guild experience a member can earn for the guild per day (0: no cap). The castle siege isn't counted.</summary>
    [Range(0, 10_000_000)]
    public int DailyCapPerMember { get; set; } = 400;

    /// <summary>Gets or sets the guild experience per monster defeated.</summary>
    public GuildXpSourceSetting MonsterKill { get; set; } = new() { Xp = 1 };

    /// <summary>Gets or sets the guild experience for being in Blood Castle, Devil Square or Chaos Castle at its end.</summary>
    public GuildXpSourceSetting EventFinish { get; set; } = new() { Xp = 30 };

    /// <summary>Gets or sets the extra guild experience for winning one of them.</summary>
    public GuildXpSourceSetting EventWin { get; set; } = new() { Xp = 60 };

    /// <summary>Gets or sets the guild experience for taking part in a world boss kill.</summary>
    public GuildXpSourceSetting WorldBossKill { get; set; } = new() { Xp = 50 };

    /// <summary>Gets or sets the guild experience per percent of a world boss's damage.</summary>
    public GuildXpSourceSetting WorldBossDamage { get; set; } = new() { Xp = 2 };

    /// <summary>Gets or sets the guild experience per mission finished.</summary>
    public GuildXpSourceSetting Mission { get; set; } = new() { Xp = 15 };

    /// <summary>Gets or sets the guild experience per <see cref="OnlineMinutes"/> in the game.</summary>
    public GuildXpSourceSetting OnlineTime { get; set; } = new() { Xp = 5 };

    /// <summary>Gets or sets the minutes in the game for <see cref="OnlineTime"/>.</summary>
    [Range(1, 1440)]
    public int OnlineMinutes { get; set; } = 10;

    /// <summary>Gets or sets the guild experience for winning the castle siege.</summary>
    public GuildXpSourceSetting CastleSiege { get; set; } = new() { Xp = 3000 };

    /// <summary>Gets or sets the extra ladder points of the castle siege winner (this week's ladder only, not its level).</summary>
    [Range(0, 10_000_000)]
    public int CastleSiegeLadderBonus { get; set; } = 5000;

    /// <summary>Gets or sets the levels, by their experience.</summary>
    public ICollection<GuildLevelDefinition> Levels { get; set; } = CreateDefaultLevels();

    /// <summary>Gets or sets a value indicating whether the online members get their guild level's buff.</summary>
    public bool BuffsEnabled { get; set; } = true;

    /// <summary>Gets or sets the English name of the buff.</summary>
    [StringLength(60)]
    public string BuffNameEn { get; set; } = "Guild blessing";

    /// <summary>Gets or sets the Vietnamese name of the buff.</summary>
    [StringLength(60)]
    public string BuffNameVi { get; set; } = "Phúc lành bang hội";

    /// <summary>Gets or sets a value indicating whether the weekly ladder pays rewards.</summary>
    public bool LadderEnabled { get; set; } = true;

    /// <summary>Gets or sets the guild experience a guild needs in a week to get a ladder reward.</summary>
    [Range(0, 10_000_000)]
    public int LadderMinimumXp { get; set; } = 100;

    /// <summary>Gets or sets the rewards of the ladder ranks.</summary>
    public ICollection<GuildLadderReward> LadderRewards { get; set; } = CreateDefaultLadderRewards();

    /// <summary>Gets or sets a value indicating whether a guild's level-up is told to its online members in the game (a system message).</summary>
    public bool AnnounceInGame { get; set; } = true;

    /// <summary>
    /// Gets or sets the file which keeps the state (guild experience, levels, this week's ladder, the finished weeks,
    /// the castle siege results, rewards waiting for room in the bag), relative to the server's folder; empty keeps it in
    /// memory only.
    /// </summary>
    [StringLength(260)]
    public string FileName { get; set; } = "../save/guilds.json";

    /// <summary>
    /// Creates the default levels (1 to 10) with small buffs.
    /// </summary>
    /// <returns>The levels.</returns>
    public static List<GuildLevelDefinition> CreateDefaultLevels()
    {
        static GuildLevelDefinition L(int level, long xp, double exp, double zen, int defense) =>
            new() { Level = level, Xp = xp, ExperiencePercent = exp, ZenPercent = zen, Defense = defense };
        return
        [
            L(1, 0, 0, 0, 0),
            L(2, 2_000, 1, 0, 0),
            L(3, 6_000, 2, 2, 0),
            L(4, 12_000, 2, 3, 5),
            L(5, 25_000, 3, 5, 10),
            L(6, 45_000, 3, 5, 15),
            L(7, 75_000, 4, 6, 20),
            L(8, 120_000, 4, 8, 25),
            L(9, 180_000, 5, 10, 30),
            L(10, 260_000, 5, 10, 40),
        ];
    }

    /// <summary>
    /// Creates the default ladder rewards (coins for the members of the top 5 guilds).
    /// </summary>
    /// <returns>The rewards.</returns>
    public static List<GuildLadderReward> CreateDefaultLadderRewards() =>
    [
        new() { RankFrom = 1, RankTo = 1, CoinsPerMember = 150, MasterBonusCoins = 100 },
        new() { RankFrom = 2, RankTo = 2, CoinsPerMember = 100, MasterBonusCoins = 50 },
        new() { RankFrom = 3, RankTo = 3, CoinsPerMember = 60, MasterBonusCoins = 30 },
        new() { RankFrom = 4, RankTo = 5, CoinsPerMember = 30 },
    ];

    /// <summary>Gets the setting of a source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The setting.</returns>
    public GuildXpSourceSetting Source(GuildXpSource source) => source switch
    {
        GuildXpSource.MonsterKill => this.MonsterKill,
        GuildXpSource.EventFinish => this.EventFinish,
        GuildXpSource.EventWin => this.EventWin,
        GuildXpSource.WorldBossKill => this.WorldBossKill,
        GuildXpSource.WorldBossDamage => this.WorldBossDamage,
        GuildXpSource.Mission => this.Mission,
        GuildXpSource.OnlineTime => this.OnlineTime,
        _ => this.CastleSiege,
    };
}
