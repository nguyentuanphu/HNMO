// <copyright file="SeasonNineEventsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineEvents;

using System.Collections.Generic;
using HNMO.DataModel.Configuration;

/// <summary>
/// Configuration for <see cref="SeasonNineEventsPlugIn"/>: Acheron Guardian, Arca Battle,
/// Chaos Castle Survival, Tormented Square, Invasion Manager, and Uruk Mountain boss guards (669-729).
/// </summary>
public sealed class SeasonNineEventsConfiguration
{
    /// <summary>Gets or sets a value indicating whether Season 9 events are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets a value indicating whether scheduled ticks only run when <c>HNMO_SEASON=s9</c>.</summary>
    public bool SeasonNineOnly { get; set; } = true;

    /// <summary>Gets or sets schedule IANA/Windows timezone identifier (default <c>Australia/Sydney</c>).</summary>
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets persistent state file path passed to <see cref="HnmoStateStore"/>.</summary>
    public string FileName { get; set; } = "save/hnmo-s9-events.json";

    // ---- 1. Acheron Guardian ----

    /// <summary>Gets or sets a value indicating whether Acheron Guardian is enabled.</summary>
    public bool AcheronGuardianEnabled { get; set; } = true;

    /// <summary>Gets or sets active weekday names (<c>Sun,Mon,Tue,Thu,Fri,Sat</c>; disabled on Arca Battle Wednesday by default).</summary>
    public string AcheronGuardianActiveDays { get; set; } = "Sun,Mon,Tue,Thu,Fri,Sat";

    /// <summary>Gets or sets daily Acheron Guardian start times (<c>HH:mm</c>).</summary>
    public List<string> AcheronGuardianDailyTimes { get; set; } = ["23:00"];

    /// <summary>Gets or sets Acheron Guardian battle duration in minutes.</summary>
    public int AcheronGuardianDurationMinutes { get; set; } = 30;

    /// <summary>Gets or sets Acheron Guardian close delay in minutes.</summary>
    public int AcheronGuardianCloseMinutes { get; set; } = 1;

    /// <summary>Gets or sets Acheron Guardian defender reward coin type.</summary>
    public CashShopCoinType AcheronGuardianRewardCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets Acheron Guardian reward coin amount.</summary>
    public int AcheronGuardianRewardCoins { get; set; } = 150;

    /// <summary>Gets or sets Acheron Guardian reward Zen amount.</summary>
    public uint AcheronGuardianRewardZen { get; set; } = 10_000_000;

    /// <summary>Gets or sets Acheron Guardian bonus item group (<c>-1</c> = none, <c>12:212</c> = Arca's Prophecy).</summary>
    public int AcheronGuardianRewardItemGroup { get; set; } = 12;

    /// <summary>Gets or sets Acheron Guardian bonus item number.</summary>
    public short AcheronGuardianRewardItemNumber { get; set; } = 212;

    // ---- 2. Arca Battle ----

    /// <summary>Gets or sets a value indicating whether Arca Battle is enabled.</summary>
    public bool ArcaBattleEnabled { get; set; } = true;

    /// <summary>Gets or sets Arca Battle active weekdays (default <c>Wed</c>).</summary>
    public string ArcaBattleActiveDays { get; set; } = "Wed";

    /// <summary>Gets or sets Arca Battle scheduled times (<c>HH:mm</c>).</summary>
    public List<string> ArcaBattleDailyTimes { get; set; } = ["22:00"];

    /// <summary>Gets or sets minimum guild members required to register Arca Battle.</summary>
    public int ArcaBattleMinGuildMembers { get; set; } = 10;

    /// <summary>Gets or sets guild master registration window in minutes.</summary>
    public int ArcaBattleMasterRegMinutes { get; set; } = 10;

    /// <summary>Gets or sets guild member sign-up window in minutes.</summary>
    public int ArcaBattleMemberRegMinutes { get; set; } = 10;

    /// <summary>Gets or sets Arca Battle battle duration in minutes.</summary>
    public int ArcaBattleDurationMinutes { get; set; } = 30;

    /// <summary>Gets or sets Arca Battle post-match close window in minutes.</summary>
    public int ArcaBattleCloseMinutes { get; set; } = 10;

    /// <summary>Gets or sets obelisk HP at Arca Battle start.</summary>
    public int ArcaBattleObeliskHp { get; set; } = 500_000;

    /// <summary>Gets or sets occupied obelisk HP.</summary>
    public int ArcaBattleOccupyHp { get; set; } = 300_000;

    /// <summary>Gets or sets Trophy of Battle Booty Mix base success % (levels 1-4).</summary>
    public int ArcaBattleTrophyMixBaseRateLow { get; set; } = 55;

    /// <summary>Gets or sets Trophy of Battle Booty Mix base success % (levels 5-7).</summary>
    public int ArcaBattleTrophyMixBaseRateMid { get; set; } = 50;

    /// <summary>Gets or sets Trophy of Battle Booty Mix base success % (levels 8-10).</summary>
    public int ArcaBattleTrophyMixBaseRateHigh { get; set; } = 45;

    /// <summary>Gets or sets added success % per extra Trophy of Battle submitted.</summary>
    public int ArcaBattleTrophyMixAddRate { get; set; } = 5;

    /// <summary>Gets or sets winning guild coin reward type.</summary>
    public CashShopCoinType ArcaBattleWinnerCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets winning guild coin reward per registered participant.</summary>
    public int ArcaBattleWinnerCoins { get; set; } = 300;

    /// <summary>Gets or sets winning guild Zen bonus.</summary>
    public uint ArcaBattleWinnerZen { get; set; } = 20_000_000;

    // ---- 3. Chaos Castle Survival ----

    /// <summary>Gets or sets a value indicating whether Chaos Castle Survival is enabled.</summary>
    public bool ChaosCastleSurvivalEnabled { get; set; } = true;

    /// <summary>Gets or sets minimum players required to begin Chaos Castle Survival.</summary>
    public int ChaosCastleSurvivalMinPlayers { get; set; } = 2;

    /// <summary>Gets or sets minimum character level to enter Chaos Castle Survival.</summary>
    public int ChaosCastleSurvivalMinLevel { get; set; } = 280;

    /// <summary>Gets or sets EXP multiplier during Chaos Castle Survival.</summary>
    public double ChaosCastleSurvivalExpMultiplier { get; set; } = 1.0;

    /// <summary>Gets or sets entry warning window in minutes.</summary>
    public int ChaosCastleSurvivalStartInMinutes { get; set; } = 5;

    /// <summary>Gets or sets combat duration in minutes.</summary>
    public int ChaosCastleSurvivalDurationMinutes { get; set; } = 10;

    /// <summary>Gets or sets day-of-month numbers (comma separated) running Elimination rounds.</summary>
    public string ChaosCastleSurvivalEliminationDays { get; set; } = "16,17,18";

    /// <summary>Gets or sets day-of-month numbers (comma separated) running Semi-Final rounds.</summary>
    public string ChaosCastleSurvivalSemiFinalDays { get; set; } = "19,20";

    /// <summary>Gets or sets day-of-month running the Final round.</summary>
    public int ChaosCastleSurvivalFinalDay { get; set; } = 21;

    /// <summary>Gets or sets Chaos Castle Survival daily schedule times (<c>HH:mm</c>).</summary>
    public List<string> ChaosCastleSurvivalDailyTimes { get; set; } =
    [
        "01:00", "02:00", "03:30", "04:00", "05:00", "06:00",
        "07:00", "08:00", "09:00", "10:00", "11:00", "12:00",
        "13:00", "14:00", "15:00", "16:00", "17:00", "18:00",
        "19:00", "20:00", "21:00", "22:00", "23:00"
    ];

    /// <summary>Gets or sets winner coin reward type.</summary>
    public CashShopCoinType ChaosCastleSurvivalWinnerCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets winner coin amount.</summary>
    public int ChaosCastleSurvivalWinnerCoins { get; set; } = 250;

    /// <summary>Gets or sets winner Zen amount.</summary>
    public uint ChaosCastleSurvivalWinnerZen { get; set; } = 15_000_000;

    /// <summary>Gets or sets final champion award item group (default 13 = Conqueror's Badge / 12 = Wing).</summary>
    public int ChaosCastleSurvivalFinalAwardItemGroup { get; set; } = 13;

    /// <summary>Gets or sets final champion award item number (default 276 Conqueror's Badge).</summary>
    public short ChaosCastleSurvivalFinalAwardItemNumber { get; set; } = 276;

    // ---- 4. Tormented Square ----

    /// <summary>Gets or sets a value indicating whether Tormented Square is enabled.</summary>
    public bool TormentedSquareEnabled { get; set; } = true;

    /// <summary>Gets or sets minimum total users to start Tormented Square.</summary>
    public int TormentedSquareMinPlayers { get; set; } = 4;

    /// <summary>Gets or sets minimum players per party.</summary>
    public int TormentedSquareMinPartyUsers { get; set; } = 2;

    /// <summary>Gets or sets minimum competing teams.</summary>
    public int TormentedSquareMinTeams { get; set; } = 2;

    /// <summary>Gets or sets minimum character level to join Tormented Square.</summary>
    public int TormentedSquareMinLevel { get; set; } = 1;

    /// <summary>Gets or sets duration per combat round in minutes.</summary>
    public int TormentedSquareRoundMinutes { get; set; } = 2;

    /// <summary>Gets or sets number of monster rounds per match (default 15).</summary>
    public int TormentedSquareRoundsCount { get; set; } = 15;

    /// <summary>Gets or sets day-of-month numbers (comma separated) for Eliminations.</summary>
    public string TormentedSquareEliminationDays { get; set; } = "17,18,19,20";

    /// <summary>Gets or sets day-of-month numbers (comma separated) for Semi-Finals.</summary>
    public string TormentedSquareSemiFinalDays { get; set; } = "21,22";

    /// <summary>Gets or sets day-of-month for the Final round.</summary>
    public int TormentedSquareFinalDay { get; set; } = 23;

    /// <summary>Gets or sets first day-of-month to claim tournament winner rewards.</summary>
    public int TormentedSquareRewardDayFrom { get; set; } = 24;

    /// <summary>Gets or sets last day-of-month to claim tournament winner rewards.</summary>
    public int TormentedSquareRewardDayTo { get; set; } = 25;

    /// <summary>Gets or sets daily Tormented Square schedule times (<c>HH:mm</c>).</summary>
    public List<string> TormentedSquareDailyTimes { get; set; } = ["17:30", "19:00", "20:30", "22:00", "23:30"];

    /// <summary>Gets or sets winning team coin reward type.</summary>
    public CashShopCoinType TormentedSquareWinnerCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets winning team coin reward per member.</summary>
    public int TormentedSquareWinnerCoins { get; set; } = 200;

    /// <summary>Gets or sets winning team Zen reward per member.</summary>
    public uint TormentedSquareWinnerZen { get; set; } = 12_000_000;

    // ---- 5. Invasion Manager ----

    /// <summary>Gets or sets a value indicating whether the Season 9 Invasion Manager is enabled.</summary>
    public bool InvasionManagerEnabled { get; set; } = true;

    /// <summary>Gets or sets configured invasion schedules (suite XMLs + S9 event mobs 674..729).</summary>
    public List<S9InvasionScheduleEntry> Invasions { get; set; } = CreateDefaultInvasions();

    // ---- 6. Uruk Mountain (Monsters 669-729) ----

    /// <summary>Gets or sets a value indicating whether Lord Silvester (673) summons elemental guards (678-680) on Uruk Mountain.</summary>
    public bool UrukMountainBossGuardsEnabled { get; set; } = true;

    /// <summary>Gets or sets number of Lord Elemental guards (678/679/680) spawned per wave.</summary>
    public int UrukMountainGuardSpawnCount { get; set; } = 3;

    /// <summary>Gets or sets cooldown in seconds between Uruk Mountain boss guard waves.</summary>
    public int UrukMountainGuardCooldownSeconds { get; set; } = 300;

    /// <summary>Gets or sets bonus coin type awarded to Lord Silvester (673) killer.</summary>
    public CashShopCoinType UrukMountainBossRewardCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets bonus coin count awarded when defeating Lord Silvester (673).</summary>
    public int UrukMountainBossRewardCoins { get; set; } = 100;

    /// <summary>Gets or sets bonus Zen awarded when defeating Lord Silvester (673).</summary>
    public uint UrukMountainBossRewardZen { get; set; } = 5_000_000;

    /// <summary>Gets or sets a value indicating whether right-clicking Evomon Summoning Scroll (14:244) spawns Evomon (681/690).</summary>
    public bool UrukMountainEvomonEnabled { get; set; } = true;

    /// <summary>Creates the default invasion manager track list matching the Season 9 server suite XMLs.</summary>
    /// <returns>Default invasion tracks.</returns>
    public static List<S9InvasionScheduleEntry> CreateDefaultInvasions() =>
    [
        new()
        {
            Id = "red-dragon",
            NameEn = "Red Dragon Invasion",
            NameVi = "Xâm Lăng Rồng Đỏ",
            MonsterNumber = 44,
            MobCount = 3,
            MapNumbers = [0, 2, 3],
            DailyTimes = ["01:00", "03:00", "13:00", "15:00", "23:00"],
            DurationMinutes = 15,
        },
        new()
        {
            Id = "skeleton-king",
            NameEn = "Skeleton King Invasion",
            NameVi = "Vua Xương Xâm Lăng",
            MonsterNumber = 55,
            MobCount = 1,
            MapNumbers = [0, 3],
            DailyTimes = ["08:00", "12:00", "20:00"],
            DurationMinutes = 15,
        },
        new()
        {
            Id = "golden-goblin",
            NameEn = "Golden Goblin Invasion",
            NameVi = "Goblin Hoàng Kim Xâm Lăng",
            MonsterNumber = 652,
            MobCount = 5,
            MapNumbers = [8, 33, 37, 91],
            DailyTimes = ["09:30", "16:30", "18:08"],
            DurationMinutes = 20,
        },
        new()
        {
            Id = "white-wizard",
            NameEn = "White Wizard Invasion",
            NameVi = "Phù Thủy Trắng Xâm Lăng",
            MonsterNumber = 135,
            MobCount = 1,
            MapNumbers = [0, 2, 3],
            DailyTimes = ["03:30", "05:30", "07:30", "11:30", "15:30", "19:30", "23:30"],
            DurationMinutes = 15,
        },
        new()
        {
            Id = "santa-village",
            NameEn = "Santa Village Attack",
            NameVi = "Sự Kiện Ông Già Noel",
            MonsterNumber = 476,
            MobCount = 2,
            MapNumbers = [0, 2, 3, 51],
            DailyTimes = ["21:56"],
            DurationMinutes = 15,
        },
        new()
        {
            Id = "s9-moon-rabbit",
            NameEn = "Moon Rabbit Invasion",
            NameVi = "Thỏ Ngọc Xâm Lăng",
            MonsterNumber = 674,
            MobCount = 10,
            MapNumbers = [0, 2, 3, 51],
            DailyTimes = ["10:30", "20:30"],
            DurationMinutes = 15,
            AnnounceDeath = false,
        },
        new()
        {
            Id = "s9-pouch-blessing",
            NameEn = "Pouch of Blessing Invasion",
            NameVi = "Túi Phúc Lộc Xâm Lăng",
            MonsterNumber = 675,
            MobCount = 10,
            MapNumbers = [4, 10, 28, 33],
            DailyTimes = ["12:30", "22:30"],
            DurationMinutes = 15,
            AnnounceDeath = false,
        },
        new()
        {
            Id = "s9-fire-flame",
            NameEn = "Fire Flame Ghost Invasion",
            NameVi = "Ma Lửa Xâm Lăng",
            MonsterNumber = 676,
            MobCount = 10,
            MapNumbers = [36, 37, 56, 57],
            DailyTimes = ["14:30", "21:30"],
            DurationMinutes = 15,
            AnnounceDeath = false,
        },
        new()
        {
            Id = "s9-horses-goats",
            NameEn = "New Year Horse & Goat Event",
            NameVi = "Sự Kiện Ngựa & Dê Năm Mới",
            MonsterNumber = 691,
            MobCount = 4,
            MapNumbers = [0, 3, 51, 79],
            DailyTimes = ["19:30"],
            DurationMinutes = 15,
            AnnounceDeath = false,
        },
    ];
}
