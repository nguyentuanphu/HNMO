// <copyright file="StatConsumableDefinitionConfig.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

/// <summary>
/// Admin-defined consumable item rule that grants extra stat points up to a configured per-character cap.
/// </summary>
public sealed class StatConsumableDefinitionConfig
{
    /// <summary>Gets or sets the item group.</summary>
    public byte ItemGroup { get; set; } = 14;

    /// <summary>Gets or sets the item number within <see cref="ItemGroup"/>.</summary>
    public short ItemNumber { get; set; }

    /// <summary>Gets or sets the English display name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese display name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the stat points granted per use.</summary>
    public int PointsPerUse { get; set; } = 10;

    /// <summary>Gets or sets an optional per-character cap override; null uses <see cref="TalismanConfiguration.GlobalStatPointsCap"/>.</summary>
    public int? MaxCapOverride { get; set; }

    /// <summary>
    /// Gets or sets where the points are added:
    /// <c>levelUpPoints</c>, <c>str</c>, <c>agi</c>, <c>vit</c>, <c>ene</c>, or <c>cmd</c>.
    /// </summary>
    public string TargetBucket { get; set; } = "levelUpPoints";
}
