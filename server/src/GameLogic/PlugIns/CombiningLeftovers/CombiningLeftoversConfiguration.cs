// <copyright file="CombiningLeftoversConfiguration.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

using System.ComponentModel.DataAnnotations;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;

/// <summary>
/// Configuration for <see cref="CombiningLeftoversPlugIn"/> (OFF by default).
/// </summary>
public sealed class CombiningLeftoversConfiguration
{
    /// <summary>Gets or sets shared HNMO state key.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets NPC number that opens Combining Leftovers &amp; Exchange when spoken to (0 = hotkey F10 only).</summary>
    [Range(0, 10000)]
    public short ExchangeNpcNumber { get; set; } = 546;

    /// <summary>Gets or sets the top allowed Box output level (overridden downward if an active Season Plan specifies a lower <c>MaxBoxLevel</c>).</summary>
    [Range(1, 15)]
    public int MaxBoxLevel { get; set; } = 5;

    /// <summary>Gets or sets configured leftover recipes.</summary>
    public List<LeftoverRecipe> Recipes { get; set; } = DefaultRecipes();

    /// <summary>Gets or sets configured surplus material exchange offers.</summary>
    public List<LeftoverExchangeOffer> ExchangeOffers { get; set; } = DefaultOffers();

    /// <summary>Standard Box of Kundun +1..+4 upgrade recipes using JoL + JoC.</summary>
    /// <returns>Default recipe list.</returns>
    public static List<LeftoverRecipe> DefaultRecipes() =>
    [
        new()
        {
            Id = "box-1-to-2",
            NameEn = "Box +1 x2 + JoL + JoC -> Box +2",
            NameVi = "Hộp +1 x2 + Sinh Mệnh + Sáng Tạo -> Hộp +2",
            SuccessRatePercent = 100,
            ZenCost = 500_000,
            OutputGroup = 14,
            OutputNumber = 11,
            OutputLevel = 9,
            RequiredStageBoxLevel = 2,
            Inputs =
            [
                new() { Group = 14, Number = 11, ExactLevel = 8, Count = 2, NameEn = "Box of Kundun +1", NameVi = "Hộp Kundun +1" },
                new() { Group = 14, Number = 16, ExactLevel = -1, Count = 1, NameEn = "Jewel of Life", NameVi = "Ngọc Sinh Mệnh" },
                new() { Group = 14, Number = 22, ExactLevel = -1, Count = 1, NameEn = "Jewel of Creation", NameVi = "Ngọc Sáng Tạo" },
            ],
        },
        new()
        {
            Id = "box-2-to-3",
            NameEn = "Box +2 x2 + JoL + JoC -> Box +3",
            NameVi = "Hộp +2 x2 + Sinh Mệnh + Sáng Tạo -> Hộp +3",
            SuccessRatePercent = 90,
            ZenCost = 1_000_000,
            OutputGroup = 14,
            OutputNumber = 11,
            OutputLevel = 10,
            RequiredStageBoxLevel = 3,
            Inputs =
            [
                new() { Group = 14, Number = 11, ExactLevel = 9, Count = 2, NameEn = "Box of Kundun +2", NameVi = "Hộp Kundun +2" },
                new() { Group = 14, Number = 16, ExactLevel = -1, Count = 1, NameEn = "Jewel of Life", NameVi = "Ngọc Sinh Mệnh" },
                new() { Group = 14, Number = 22, ExactLevel = -1, Count = 1, NameEn = "Jewel of Creation", NameVi = "Ngọc Sáng Tạo" },
            ],
        },
        new()
        {
            Id = "box-3-to-4",
            NameEn = "Box +3 x2 + JoL + JoC -> Box +4",
            NameVi = "Hộp +3 x2 + Sinh Mệnh + Sáng Tạo -> Hộp +4",
            SuccessRatePercent = 85,
            ZenCost = 2_000_000,
            OutputGroup = 14,
            OutputNumber = 11,
            OutputLevel = 11,
            RequiredStageBoxLevel = 4,
            Inputs =
            [
                new() { Group = 14, Number = 11, ExactLevel = 10, Count = 2, NameEn = "Box of Kundun +3", NameVi = "Hộp Kundun +3" },
                new() { Group = 14, Number = 16, ExactLevel = -1, Count = 1, NameEn = "Jewel of Life", NameVi = "Ngọc Sinh Mệnh" },
                new() { Group = 14, Number = 22, ExactLevel = -1, Count = 1, NameEn = "Jewel of Creation", NameVi = "Ngọc Sáng Tạo" },
            ],
        },
        new()
        {
            Id = "box-4-to-5",
            NameEn = "Box +4 x2 + JoL + JoC -> Box +5",
            NameVi = "Hộp +4 x2 + Sinh Mệnh + Sáng Tạo -> Hộp +5",
            SuccessRatePercent = 80,
            ZenCost = 3_000_000,
            OutputGroup = 14,
            OutputNumber = 11,
            OutputLevel = 12,
            RequiredStageBoxLevel = 5,
            Inputs =
            [
                new() { Group = 14, Number = 11, ExactLevel = 11, Count = 2, NameEn = "Box of Kundun +4", NameVi = "Hộp Kundun +4" },
                new() { Group = 14, Number = 16, ExactLevel = -1, Count = 1, NameEn = "Jewel of Life", NameVi = "Ngọc Sinh Mệnh" },
                new() { Group = 14, Number = 22, ExactLevel = -1, Count = 1, NameEn = "Jewel of Creation", NameVi = "Ngọc Sáng Tạo" },
            ],
        },
    ];

    /// <summary>Default surplus material exchanges.</summary>
    /// <returns>Default exchange offers.</returns>
    public static List<LeftoverExchangeOffer> DefaultOffers() =>
    [
        new()
        {
            Id = "ex-rena",
            NameEn = "5 × Rena -> 25 Goblin Points",
            NameVi = "Đổi 5 Rena -> 25 Goblin Points",
            InputGroup = 14,
            InputNumber = 21,
            InputLevel = 0,
            InputCount = 5,
            RewardCoins = 25,
            RewardCoinType = CashShopCoinType.GoblinPoints,
        },
        new()
        {
            Id = "ex-sign",
            NameEn = "5 × Sign of Lord -> 40 Goblin Points",
            NameVi = "Đổi 5 Huy hiệu Chúa Tể -> 40 Goblin Points",
            InputGroup = 14,
            InputNumber = 21,
            InputLevel = 3,
            InputCount = 5,
            RewardCoins = 40,
            RewardCoinType = CashShopCoinType.GoblinPoints,
        },
    ];
}
