// <copyright file="TalismanDefinitionConfig.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

/// <summary>
/// Admin-defined talisman or figurine rule granting passive bonuses or a magic effect buff while carried in the bag.
/// </summary>
public sealed class TalismanDefinitionConfig
{
    /// <summary>Gets or sets the item group (e.g. 13 or 14).</summary>
    public byte ItemGroup { get; set; } = 13;

    /// <summary>Gets or sets the item number within <see cref="ItemGroup"/>.</summary>
    public short ItemNumber { get; set; }

    /// <summary>Gets or sets an optional minimum item level required to activate bonuses.</summary>
    public short? MinItemLevel { get; set; }

    /// <summary>Gets or sets the English display name.</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese display name.</summary>
    public string NameVi { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether carrying multiple copies stacks bonuses.</summary>
    public bool StackCopies { get; set; }

    /// <summary>Gets or sets the maximum stacked copies when <see cref="StackCopies"/> is <c>true</c>.</summary>
    public int MaxStacks { get; set; } = 1;

    /// <summary>Gets or sets extra experience rate percentage (e.g. <c>10</c> adds +10%).</summary>
    public float BonusExpPercent { get; set; }

    /// <summary>Gets or sets extra Zen drop percentage (e.g. <c>10</c> adds +10%).</summary>
    public float BonusZenPercent { get; set; }

    /// <summary>Gets or sets flat bonus damage while carried.</summary>
    public int BonusDamage { get; set; }

    /// <summary>Gets or sets flat bonus defense while carried.</summary>
    public int BonusDefense { get; set; }

    /// <summary>Gets or sets flat bonus maximum health while carried.</summary>
    public int BonusMaxHp { get; set; }

    /// <summary>Gets or sets bonus attack speed while carried.</summary>
    public int BonusAttackSpeed { get; set; }

    /// <summary>Gets or sets critical damage chance bonus in percent.</summary>
    public float CriticalChancePercent { get; set; }

    /// <summary>Gets or sets an optional <c>MagicEffectDefinition.Number</c> applied while carried.</summary>
    public short? GrantedMagicEffectNumber { get; set; }
}
