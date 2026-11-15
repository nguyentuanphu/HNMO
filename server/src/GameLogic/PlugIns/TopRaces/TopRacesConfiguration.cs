// <copyright file="TopRacesConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Text.Json.Serialization;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// What a top race counts.
/// </summary>
public enum TopRaceKind
{
    /// <summary>Resets done during the race.</summary>
    ResetsGained,

    /// <summary>Levels gained during the race (a reset doesn't take any away).</summary>
    LevelsGained,

    /// <summary>The highest level reached during the race (a tie goes to whoever got there first).</summary>
    LevelReached,

    /// <summary>The world bosses' boss-hunt points (each boss's points shared by damage).</summary>
    BossHuntPoints,

    /// <summary>Other players defeated (not in duels or mini games).</summary>
    PkKills,

    /// <summary>Blood Castle, Devil Square or Chaos Castle won.</summary>
    EventWins,

    /// <summary>The guild experience a guild earned during the race; the prizes go to every member.</summary>
    GuildPoints,

    /// <summary>Monsters defeated.</summary>
    MonsterKills,

    /// <summary>Minutes in the game.</summary>
    OnlineMinutes,
}

/// <summary>
/// Which events count for <see cref="TopRaceKind.EventWins"/>.
/// </summary>
public enum TopRaceEvent
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
/// When a top race runs.
/// </summary>
public enum TopRacePeriod
{
    /// <summary>Every day, from 00:00 (Sydney time by default).</summary>
    Daily,

    /// <summary>Every week, from Monday 00:00.</summary>
    Weekly,

    /// <summary>Every month, from the 1st 00:00.</summary>
    Monthly,

    /// <summary>Once, from <see cref="TopRaceDefinition.Start"/> to <see cref="TopRaceDefinition.End"/>.</summary>
    Custom,
}

/// <summary>
/// Who takes part in a top race.
/// </summary>
public enum TopRaceScope
{
    /// <summary>Every class in one table.</summary>
    AllClasses,

    /// <summary>Only one class (<see cref="TopRaceDefinition.Class"/>).</summary>
    OneClass,

    /// <summary>A table (and prizes) for each class.</summary>
    PerClass,
}

/// <summary>
/// The classes, each with its later evolutions (e.g. the Dark Wizard with the Soul Master and Grand Master).
/// </summary>
public enum TopRaceClass
{
    /// <summary>Dark Wizard, Soul Master, Grand Master.</summary>
    DarkWizard = 0,

    /// <summary>Dark Knight, Blade Knight, Blade Master.</summary>
    DarkKnight = 1,

    /// <summary>Fairy Elf, Muse Elf, High Elf.</summary>
    FairyElf = 2,

    /// <summary>Magic Gladiator, Duel Master.</summary>
    MagicGladiator = 3,

    /// <summary>Dark Lord, Lord Emperor.</summary>
    DarkLord = 4,

    /// <summary>Summoner, Bloody Summoner, Dimension Master.</summary>
    Summoner = 5,

    /// <summary>Rage Fighter, Fist Master.</summary>
    RageFighter = 6,
}

/// <summary>
/// The prize of a range of ranks.
/// </summary>
public class TopRacePrize
{
    /// <summary>Gets or sets the first rank.</summary>
    [Range(1, 100)]
    public int RankFrom { get; set; } = 1;

    /// <summary>Gets or sets the last rank.</summary>
    [Range(1, 100)]
    public int RankTo { get; set; } = 1;

    /// <summary>Gets or sets the coins (to the account; for a guild race, to each member's account).</summary>
    [Range(0, 1_000_000)]
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; } = CashShopCoinType.GoblinPoints;

    /// <summary>Gets or sets the zen (to the character; for a guild race, to each member).</summary>
    [Range(0, 2_000_000_000)]
    public int Zen { get; set; }

    /// <summary>Gets or sets the items.</summary>
    public ICollection<MissionItemReward> Items { get; set; } = new List<MissionItemReward>();

    /// <summary>Gets a value indicating whether the prize gives nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => this.Coins <= 0 && this.Zen <= 0 && this.Items.All(i => i.Count <= 0);
}

/// <summary>
/// A top race.
/// </summary>
public class TopRaceDefinition
{
    /// <summary>Gets or sets the identifier; the standings are kept by it, so it shouldn't change while it runs.</summary>
    [Required]
    [StringLength(40, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9_-]+$")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the race runs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the English title.</summary>
    [StringLength(80)]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    [StringLength(80)]
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets what it counts.</summary>
    public TopRaceKind Kind { get; set; }

    /// <summary>Gets or sets which events count (event wins only).</summary>
    public TopRaceEvent Event { get; set; }

    /// <summary>Gets or sets who takes part (not for a guild race).</summary>
    public TopRaceScope Scope { get; set; }

    /// <summary>Gets or sets the class of <see cref="TopRaceScope.OneClass"/>.</summary>
    public TopRaceClass Class { get; set; }

    /// <summary>Gets or sets when it runs.</summary>
    public TopRacePeriod Period { get; set; } = TopRacePeriod.Weekly;

    /// <summary>Gets or sets the start of a custom race, "yyyy-MM-dd HH:mm" on the wall clock of the time zone (Sydney).</summary>
    [StringLength(20)]
    public string Start { get; set; } = string.Empty;

    /// <summary>Gets or sets the end of a custom race, "yyyy-MM-dd HH:mm" on the wall clock of the time zone (Sydney).</summary>
    [StringLength(20)]
    public string End { get; set; } = string.Empty;

    /// <summary>Gets or sets the value needed for a prize (at least 1).</summary>
    [Range(1, long.MaxValue)]
    public long Minimum { get; set; } = 1;

    /// <summary>Gets or sets the prizes by rank (for each class's table with <see cref="TopRaceScope.PerClass"/>).</summary>
    public ICollection<TopRacePrize> Prizes { get; set; } = new List<TopRacePrize>();

    /// <summary>Gets the Vietnamese title, or the English one when it's empty.</summary>
    [JsonIgnore]
    public string TitleViOrEn => string.IsNullOrWhiteSpace(this.TitleVi) ? this.TitleEn : this.TitleVi;

    /// <summary>Gets a value indicating whether the race ranks guilds instead of characters.</summary>
    [JsonIgnore]
    public bool IsGuildRace => this.Kind == TopRaceKind.GuildPoints;
}

/// <summary>
/// The configuration of HNMO's top races: races by day, week, month or between two dates, each with its own prizes.
/// </summary>
public class TopRacesConfiguration
{
    /// <summary>Gets or sets the time zone of the days, weeks, months and custom dates, an IANA name.</summary>
    [StringLength(64)]
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets the races.</summary>
    public ICollection<TopRaceDefinition> Races { get; set; } = CreateDefaultRaces();

    /// <summary>Gets or sets a value indicating whether a race's start and end are shown to everyone in the game (a golden notice).</summary>
    public bool AnnounceInGame { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a race's start and winners are posted through the community (Discord and the website's highlights).</summary>
    public bool AnnounceOnCommunity { get; set; } = true;

    /// <summary>Gets or sets the English start notice; placeholders {race} and {end}.</summary>
    [StringLength(300)]
    public string StartTextEn { get; set; } = "The top race \"{race}\" has started! It ends {end} (Sydney time).";

    /// <summary>Gets or sets the Vietnamese start notice.</summary>
    [StringLength(300)]
    public string StartTextVi { get; set; } = "Đua top \"{race}\" đã bắt đầu! Kết thúc lúc {end} (giờ Sydney).";

    /// <summary>Gets or sets the English end notice; placeholders {race}, {winner} and {value}.</summary>
    [StringLength(300)]
    public string EndTextEn { get; set; } = "The top race \"{race}\" has ended. Winner: {winner} ({value})!";

    /// <summary>Gets or sets the Vietnamese end notice.</summary>
    [StringLength(300)]
    public string EndTextVi { get; set; } = "Đua top \"{race}\" đã kết thúc. Người thắng: {winner} ({value})!";

    /// <summary>Gets or sets how many finished races the history keeps.</summary>
    [Range(1, 500)]
    public int HistoryKept { get; set; } = 40;

    /// <summary>
    /// Gets or sets the file which keeps the state (the running races' standings, the finished races, the prizes waiting
    /// for room in the bag), relative to the server's folder; empty keeps it in memory only.
    /// </summary>
    [StringLength(260)]
    public string FileName { get; set; } = "../save/top-races.json";

    /// <summary>
    /// Creates the default races: weekly resets and boss hunt, monthly levels per class, daily monsters and weekly event
    /// wins, with modest coin prizes; a weekly PK race is there but off.
    /// </summary>
    /// <returns>The races.</returns>
    public static List<TopRaceDefinition> CreateDefaultRaces()
    {
        static TopRacePrize P(int from, int to, int coins, int zen = 0, params MissionItemReward[] items) =>
            new() { RankFrom = from, RankTo = to, Coins = coins, Zen = zen, Items = items.ToList() };
        var bless = new MissionItemReward { Group = 14, Number = 13, Count = 1 };
        var soul = new MissionItemReward { Group = 14, Number = 14, Count = 1 };
        return
        [
            new()
            {
                Id = "weekly-resets", TitleEn = "Weekly reset race", TitleVi = "Đua top reset tuần", Kind = TopRaceKind.ResetsGained,
                Period = TopRacePeriod.Weekly, Minimum = 1,
                Prizes = [P(1, 1, 300), P(2, 2, 200), P(3, 3, 120), P(4, 10, 30)],
            },
            new()
            {
                Id = "weekly-boss-hunt", TitleEn = "Weekly boss hunt", TitleVi = "Đua top săn boss tuần", Kind = TopRaceKind.BossHuntPoints,
                Period = TopRacePeriod.Weekly, Minimum = 10,
                Prizes = [P(1, 1, 200), P(2, 2, 120), P(3, 3, 80), P(4, 10, 20)],
            },
            new()
            {
                Id = "monthly-levels", TitleEn = "Monthly level race (per class)", TitleVi = "Đua top cấp tháng (theo lớp nhân vật)", Kind = TopRaceKind.LevelsGained,
                Scope = TopRaceScope.PerClass, Period = TopRacePeriod.Monthly, Minimum = 50,
                Prizes = [P(1, 1, 250, 0, bless, soul), P(2, 2, 150, 0, bless), P(3, 3, 100)],
            },
            new()
            {
                Id = "daily-monsters", TitleEn = "Daily monster hunt", TitleVi = "Đua top săn quái ngày", Kind = TopRaceKind.MonsterKills,
                Period = TopRacePeriod.Daily, Minimum = 200,
                Prizes = [P(1, 1, 50), P(2, 2, 30), P(3, 3, 20)],
            },
            new()
            {
                Id = "weekly-events", TitleEn = "Weekly event champions", TitleVi = "Đua top sự kiện tuần", Kind = TopRaceKind.EventWins,
                Event = TopRaceEvent.Any, Period = TopRacePeriod.Weekly, Minimum = 1,
                Prizes = [P(1, 1, 120), P(2, 2, 80), P(3, 3, 50)],
            },
            new()
            {
                Id = "weekly-pk", Enabled = false, TitleEn = "Weekly PK race", TitleVi = "Đua top PK tuần", Kind = TopRaceKind.PkKills,
                Period = TopRacePeriod.Weekly, Minimum = 5,
                Prizes = [P(1, 1, 100), P(2, 2, 60), P(3, 3, 30)],
            },
        ];
    }
}
