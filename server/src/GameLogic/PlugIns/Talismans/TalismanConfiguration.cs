// <copyright file="TalismanConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

using HNMO.Persistence;

/// <summary>
/// Configuration for <see cref="TalismanPlugIn"/> (OFF by default until enabled by the host).
/// </summary>
public sealed class TalismanConfiguration
{
    /// <summary>
    /// Gets or sets the default per-character maximum extra stat points claimable from stat consumables.
    /// </summary>
    public int GlobalStatPointsCap { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the state file key passed to <see cref="HnmoStateStore"/> / row persistence.
    /// </summary>
    public string FileName { get; set; } = HnmoStateStore.StateFileName;

    /// <summary>
    /// Gets or sets the admin-defined talismans/figurines giving bonuses or buffs while carried in the bag.
    /// </summary>
    public List<TalismanDefinitionConfig> Talismans { get; set; } =
    [
        new TalismanDefinitionConfig
        {
            ItemGroup = 13,
            ItemNumber = 81,
            NameEn = "Guardian Talisman",
            NameVi = "Bùa Hộ Mệnh",
            BonusDefense = 50,
            BonusMaxHp = 150,
            BonusExpPercent = 5,
        },
        new TalismanDefinitionConfig
        {
            ItemGroup = 14,
            ItemNumber = 53,
            NameEn = "Fortune Charm",
            NameVi = "Bùa May Mắn",
            BonusExpPercent = 5,
            BonusZenPercent = 10,
            BonusDamage = 25,
        },
    ];

    /// <summary>
    /// Gets or sets the admin-defined consumable items that grant extra stat points up to a cap.
    /// </summary>
    public List<StatConsumableDefinitionConfig> StatConsumables { get; set; } =
    [
        new StatConsumableDefinitionConfig
        {
            ItemGroup = 14,
            ItemNumber = 91,
            NameEn = "Stat Scroll (+10)",
            NameVi = "Sách Tiềm Năng (+10)",
            PointsPerUse = 10,
            TargetBucket = "levelUpPoints",
        },
    ];
}
