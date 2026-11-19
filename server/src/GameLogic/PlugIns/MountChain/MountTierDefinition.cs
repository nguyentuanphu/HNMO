// <copyright file="MountTierDefinition.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.MountChain;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Definition of one mount tier in the progressive mount chain.
/// </summary>
public sealed class MountTierDefinition
{
    /// <summary>Gets or sets unique tier ID.</summary>
    [StringLength(32)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets English name.</summary>
    [StringLength(64)]
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets Vietnamese name.</summary>
    [StringLength(64)]
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets item group for the mount item (e.g. 13 = helpers/mounts).</summary>
    [Range(0, 15)]
    public byte ItemGroup { get; set; } = 13;

    /// <summary>Gets or sets item number for the mount item.</summary>
    [Range(0, 511)]
    public short ItemNumber { get; set; } = 2;

    /// <summary>Gets or sets item level/variant.</summary>
    [Range(0, 15)]
    public byte ItemLevel { get; set; }

    /// <summary>Gets or sets required Mount Shards earned from golden bosses / salvaging duplicates.</summary>
    [Range(0, 10000)]
    public int ShardsRequired { get; set; } = 3;

    /// <summary>Gets or sets previous tier ID whose bag mount item is consumed (empty for first tier).</summary>
    [StringLength(32)]
    public string PreviousTierId { get; set; } = string.Empty;

    /// <summary>Gets or sets number of previous-tier duplicate mount items consumed to forge this tier.</summary>
    [Range(0, 10)]
    public int PreviousMountsRequired { get; set; }

    /// <summary>Gets or sets Zen cost.</summary>
    [Range(0, 2_000_000_000)]
    public int ZenCost { get; set; } = 1_000_000;

    /// <summary>Gets or sets shards awarded when salvaging a duplicate copy of this tier.</summary>
    [Range(1, 500)]
    public int ShardsOnSalvage { get; set; } = 2;

    /// <summary>Gets or sets modest PvE defense bonus when active.</summary>
    [Range(0, 500)]
    public int PveDefenseBonus { get; set; } = 5;

    /// <summary>Gets or sets modest XP bonus percentage (0..50).</summary>
    [Range(0, 50)]
    public int BonusExpPercent { get; set; }

    /// <summary>Gets or sets modest Zen drop bonus percentage (0..100).</summary>
    [Range(0, 100)]
    public int BonusZenPercent { get; set; } = 5;
}
