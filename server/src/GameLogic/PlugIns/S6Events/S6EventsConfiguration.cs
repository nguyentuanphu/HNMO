// <copyright file="S6EventsConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System.Collections.Generic;
using HNMO.DataModel.Configuration;

/// <summary>
/// Configuration for <see cref="S6EventsPlugIn"/> (disabled by default): Last Man Standing arena event,
/// Cherry Blossom Spirit exchange, and Delgado Lucky Coin registration.
/// </summary>
public sealed class S6EventsConfiguration
{
    /// <summary>Gets or sets a value indicating whether Season 6 events and exchanges are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets schedule IANA/Windows timezone identifier (default <c>Australia/Sydney</c>).</summary>
    public string TimeZone { get; set; } = "Australia/Sydney";

    /// <summary>Gets or sets state file path passed to <see cref="HnmoStateStore"/>.</summary>
    public string FileName { get; set; } = "save/hnmo-s6-events.json";

    /// <summary>Gets or sets a value indicating whether Last Man Standing scheduled rounds are enabled.</summary>
    public bool LmsEnabled { get; set; } = true;

    /// <summary>Gets or sets daily start times in <see cref="TimeZone"/> (<c>HH:mm</c>).</summary>
    public List<string> LmsDailyTimes { get; set; } = ["20:30"];

    /// <summary>Gets or sets registration open window in minutes before PvP begins.</summary>
    public int LmsRegistrationMinutes { get; set; } = 3;

    /// <summary>Gets or sets combat round duration in minutes.</summary>
    public int LmsDurationMinutes { get; set; } = 10;

    /// <summary>Gets or sets minimum contestants required for an LMS round to begin (2..50).</summary>
    public int LmsMinPlayers { get; set; } = 2;

    /// <summary>Gets or sets minimum character level to join LMS.</summary>
    public int LmsMinLevel { get; set; } = 50;

    /// <summary>Gets or sets optional Zen entry fee deducted upon joining via <c>/lms</c>.</summary>
    public int LmsEntryFeeZen { get; set; }

    /// <summary>Gets or sets LMS battle map number (default 64 = Duel Arena; falls back to 0 if unavailable).</summary>
    public short LmsArenaMapNumber { get; set; } = 64;

    /// <summary>Gets or sets LMS spawn X coordinate.</summary>
    public byte LmsArenaX { get; set; } = 140;

    /// <summary>Gets or sets LMS spawn Y coordinate.</summary>
    public byte LmsArenaY { get; set; } = 125;

    /// <summary>Gets or sets safe return map number after elimination or win.</summary>
    public short LmsReturnMapNumber { get; set; }

    /// <summary>Gets or sets return map X coordinate.</summary>
    public byte LmsReturnX { get; set; } = 130;

    /// <summary>Gets or sets return map Y coordinate.</summary>
    public byte LmsReturnY { get; set; } = 130;

    /// <summary>Gets or sets LMS winner coin reward type.</summary>
    public CashShopCoinType LmsRewardCoinType { get; set; } = CashShopCoinType.WCoinC;

    /// <summary>Gets or sets LMS winner coin amount granted via <see cref="Missions.MissionCoinGranter"/>.</summary>
    public int LmsRewardCoins { get; set; } = 100;

    /// <summary>Gets or sets LMS winner Zen reward queued via owed rewards.</summary>
    public uint LmsRewardZen { get; set; } = 5_000_000;

    /// <summary>Gets or sets optional LMS bonus item group (-1 = none, e.g. 14 = Misc/Jewels).</summary>
    public int LmsRewardItemGroup { get; set; } = 14;

    /// <summary>Gets or sets optional LMS bonus item number (default 14 = Jewel of Soul).</summary>
    public short LmsRewardItemNumber { get; set; } = 14;

    /// <summary>Gets or sets optional LMS bonus item level.</summary>
    public byte LmsRewardItemLevel { get; set; }

    /// <summary>Gets or sets optional LMS bonus item count.</summary>
    public int LmsRewardItemCount { get; set; } = 1;

    /// <summary>Gets or sets a value indicating whether Cherry Blossom exchanges are enabled.</summary>
    public bool CherryBlossomEnabled { get; set; } = true;

    /// <summary>Gets or sets Cherry Blossom NPC number (default 450).</summary>
    public short CherryBlossomNpcNumber { get; set; } = 450;

    /// <summary>Gets or sets Golden Cherry Blossom Branch item group (default 14).</summary>
    public int CherryBlossomItemGroup { get; set; } = 14;

    /// <summary>Gets or sets Golden Cherry Blossom Branch item number (default 90).</summary>
    public short CherryBlossomItemNumber { get; set; } = 90;

    /// <summary>Gets or sets configured Cherry Blossom tiers.</summary>
    public List<S6ExchangeTier> CherryBlossomTiers { get; set; } = CreateDefaultCherryBlossomTiers();

    /// <summary>Gets or sets a value indicating whether Delgado Lucky Coin exchanges are enabled.</summary>
    public bool LuckyCoinEnabled { get; set; } = true;

    /// <summary>Gets or sets Delgado NPC number (default 478).</summary>
    public short LuckyCoinNpcNumber { get; set; } = 478;

    /// <summary>Gets or sets Lucky Coin item group (default 14).</summary>
    public int LuckyCoinItemGroup { get; set; } = 14;

    /// <summary>Gets or sets Lucky Coin item number (default 100).</summary>
    public short LuckyCoinItemNumber { get; set; } = 100;

    /// <summary>Gets or sets configured Lucky Coin exchange tiers.</summary>
    public List<S6ExchangeTier> LuckyCoinTiers { get; set; } = CreateDefaultLuckyCoinTiers();

    /// <summary>Creates default Cherry Blossom tiers (10 / 50 / 255 stacked branches).</summary>
    /// <returns>Initialized list of exchange tiers.</returns>
    public static List<S6ExchangeTier> CreateDefaultCherryBlossomTiers() =>
    [
        new()
        {
            RequiredCount = 10,
            LabelEn = "10 Branches (Wine / Rice Cake / Zen)",
            LabelVi = "10 Cành Đào (Rượu / Bánh / Zen)",
            Rewards =
            [
                new() { Weight = 50, NameEn = "Cherry Blossom Wine + 500k Zen", NameVi = "Rượu Hoa Đào + 500k Zen", Zen = 500_000, ItemGroup = 14, ItemNumber = 85, ItemCount = 1 },
                new() { Weight = 50, NameEn = "Cherry Blossom Rice Cake + 500k Zen", NameVi = "Bánh Chưng Hoa Đào + 500k Zen", Zen = 500_000, ItemGroup = 14, ItemNumber = 86, ItemCount = 1 },
            ],
        },
        new()
        {
            RequiredCount = 50,
            LabelEn = "50 Branches (Flower Petal + 20 WCoinC)",
            LabelVi = "50 Cành Đào (Cánh Hoa Đào + 20 WCoinC)",
            Rewards =
            [
                new() { Weight = 100, NameEn = "Cherry Blossom Flower Petal + 20 WCoinC", NameVi = "Cánh Hoa Đào + 20 WCoinC", Coins = 20, CoinType = CashShopCoinType.WCoinC, ItemGroup = 14, ItemNumber = 87, ItemCount = 1 },
            ],
        },
        new()
        {
            RequiredCount = 255,
            LabelEn = "255 Branches (Grand Jewel / 100 WCoinC)",
            LabelVi = "255 Cành Đào (Ngọc Quý / 100 WCoinC)",
            Rewards =
            [
                new() { Weight = 60, NameEn = "Jewel of Soul + 100 WCoinC", NameVi = "Ngọc Tâm Linh + 100 WCoinC", Coins = 100, CoinType = CashShopCoinType.WCoinC, ItemGroup = 14, ItemNumber = 14, ItemCount = 1 },
                new() { Weight = 40, NameEn = "Jewel of Life + 100 WCoinC", NameVi = "Ngọc Sinh Mệnh + 100 WCoinC", Coins = 100, CoinType = CashShopCoinType.WCoinC, ItemGroup = 14, ItemNumber = 16, ItemCount = 1 },
            ],
        },
    ];

    /// <summary>Creates default Lucky Coin tiers (10 / 20 / 30 coins).</summary>
    /// <returns>Initialized list of exchange tiers.</returns>
    public static List<S6ExchangeTier> CreateDefaultLuckyCoinTiers() =>
    [
        new()
        {
            RequiredCount = 10,
            LabelEn = "10 Lucky Coins (1M Zen + 10 WCoinC)",
            LabelVi = "10 Xu May Mắn (1M Zen + 10 WCoinC)",
            Rewards =
            [
                new() { Weight = 100, NameEn = "1,000,000 Zen + 10 WCoinC", NameVi = "1,000,000 Zen + 10 WCoinC", Zen = 1_000_000, Coins = 10, CoinType = CashShopCoinType.WCoinC },
            ],
        },
        new()
        {
            RequiredCount = 20,
            LabelEn = "20 Lucky Coins (Jewel of Bless + 25 WCoinC)",
            LabelVi = "20 Xu May Mắn (Ngọc Ước Nguyện + 25 WCoinC)",
            Rewards =
            [
                new() { Weight = 100, NameEn = "Jewel of Bless + 25 WCoinC", NameVi = "Ngọc Ước Nguyện + 25 WCoinC", Coins = 25, CoinType = CashShopCoinType.WCoinC, ItemGroup = 14, ItemNumber = 13, ItemCount = 1 },
            ],
        },
        new()
        {
            RequiredCount = 30,
            LabelEn = "30 Lucky Coins (Jewel of Creation + 60 WCoinC)",
            LabelVi = "30 Xu May Mắn (Ngọc Sáng Tạo + 60 WCoinC)",
            Rewards =
            [
                new() { Weight = 100, NameEn = "Jewel of Creation + 60 WCoinC", NameVi = "Ngọc Sáng Tạo + 60 WCoinC", Coins = 60, CoinType = CashShopCoinType.WCoinC, ItemGroup = 14, ItemNumber = 22, ItemCount = 1 },
            ],
        },
    ];
}
