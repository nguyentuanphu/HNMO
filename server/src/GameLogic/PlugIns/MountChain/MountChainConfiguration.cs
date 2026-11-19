// <copyright file="MountChainConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="MountChainPlugIn"/> (OFF by default).
/// </summary>
public sealed class MountChainConfiguration
{
    /// <summary>Gets or sets shared HNMO state key.</summary>
    [StringLength(260)]
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>Gets or sets percentage chance (1..100) for a Golden Boss kill to award Mount Shards.</summary>
    [Range(1, 100)]
    public int GoldenDropChancePercent { get; set; } = 100;

    /// <summary>Gets or sets Mount Shards granted per Golden Boss drop.</summary>
    [Range(1, 50)]
    public int ShardsPerGoldenBoss { get; set; } = 2;

    /// <summary>Gets or sets monster numbers always recognized as Golden Bosses in addition to "Golden" designation matches.</summary>
    public List<int> GoldenMonsterNumbers { get; set; } = [43, 53, 54, 78, 79, 80, 81, 82, 83, 493, 494, 495, 496, 497, 498, 499, 500, 501, 502];

    /// <summary>Gets or sets progressive mount upgrade chain tiers.</summary>
    public List<MountTierDefinition> Tiers { get; set; } = DefaultTiers();

    /// <summary>Creates default progressive mount chain (Uniria -> Dinorant -> Fenrir -> Golden Fenrir -> Royal Steed).</summary>
    /// <returns>Default tier list.</returns>
    public static List<MountTierDefinition> DefaultTiers() =>
    [
        new()
        {
            Id = "horn-uniria",
            NameEn = "Horn of Uniria",
            NameVi = "Kỳ Lân (Uniria)",
            ItemGroup = 13,
            ItemNumber = 2,
            ItemLevel = 0,
            ShardsRequired = 3,
            PreviousTierId = string.Empty,
            PreviousMountsRequired = 0,
            ZenCost = 500_000,
            ShardsOnSalvage = 2,
            PveDefenseBonus = 5,
            BonusExpPercent = 2,
            BonusZenPercent = 5,
        },
        new()
        {
            Id = "dinorant",
            NameEn = "Horn of Dinorant",
            NameVi = "Quái Điểu (Dinorant)",
            ItemGroup = 13,
            ItemNumber = 3,
            ItemLevel = 0,
            ShardsRequired = 6,
            PreviousTierId = "horn-uniria",
            PreviousMountsRequired = 1,
            ZenCost = 1_500_000,
            ShardsOnSalvage = 4,
            PveDefenseBonus = 12,
            BonusExpPercent = 4,
            BonusZenPercent = 10,
        },
        new()
        {
            Id = "fenrir-wolf",
            NameEn = "Horn of Fenrir",
            NameVi = "Sói Tinh (Fenrir)",
            ItemGroup = 13,
            ItemNumber = 37,
            ItemLevel = 0,
            ShardsRequired = 12,
            PreviousTierId = "dinorant",
            PreviousMountsRequired = 1,
            ZenCost = 3_000_000,
            ShardsOnSalvage = 8,
            PveDefenseBonus = 20,
            BonusExpPercent = 6,
            BonusZenPercent = 15,
        },
        new()
        {
            Id = "golden-wolf",
            NameEn = "Golden Fenrir",
            NameVi = "Sói Hoàng Kim",
            ItemGroup = 13,
            ItemNumber = 37,
            ItemLevel = 4,
            ShardsRequired = 20,
            PreviousTierId = "fenrir-wolf",
            PreviousMountsRequired = 2,
            ZenCost = 6_000_000,
            ShardsOnSalvage = 12,
            PveDefenseBonus = 30,
            BonusExpPercent = 8,
            BonusZenPercent = 20,
        },
        new()
        {
            Id = "royal-steed",
            NameEn = "Royal Steed",
            NameVi = "Chiến Mã Hoàng Gia",
            ItemGroup = 13,
            ItemNumber = 4,
            ItemLevel = 0,
            ShardsRequired = 30,
            PreviousTierId = "golden-wolf",
            PreviousMountsRequired = 1,
            ZenCost = 10_000_000,
            ShardsOnSalvage = 16,
            PveDefenseBonus = 40,
            BonusExpPercent = 10,
            BonusZenPercent = 25,
        },
    ];
}
