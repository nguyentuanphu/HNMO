// <copyright file="BuildCalculatorResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BuildCalculator;

/// <summary>
/// Exact computed character attributes produced by the server's <see cref="HNMO.GameLogic.Attributes.ItemAwareAttributeSystem"/>.
/// </summary>
public sealed class BuildCalculatorResult
{
    /// <summary>Gets or sets character class name.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Gets or sets effective total Strength.</summary>
    public int TotalStrength { get; set; }

    /// <summary>Gets or sets effective total Agility.</summary>
    public int TotalAgility { get; set; }

    /// <summary>Gets or sets effective total Vitality.</summary>
    public int TotalVitality { get; set; }

    /// <summary>Gets or sets effective total Energy.</summary>
    public int TotalEnergy { get; set; }

    /// <summary>Gets or sets effective total Leadership.</summary>
    public int TotalLeadership { get; set; }

    /// <summary>Gets or sets minimum physical base damage.</summary>
    public int MinPhysDmg { get; set; }

    /// <summary>Gets or sets maximum physical base damage.</summary>
    public int MaxPhysDmg { get; set; }

    /// <summary>Gets or sets minimum wizardry base damage.</summary>
    public int MinWizDmg { get; set; }

    /// <summary>Gets or sets maximum wizardry base damage.</summary>
    public int MaxWizDmg { get; set; }

    /// <summary>Gets or sets minimum curse base damage.</summary>
    public int MinCurseDmg { get; set; }

    /// <summary>Gets or sets maximum curse base damage.</summary>
    public int MaxCurseDmg { get; set; }

    /// <summary>Gets or sets base defense.</summary>
    public int DefenseBase { get; set; }

    /// <summary>Gets or sets total PvM defense.</summary>
    public int DefensePvm { get; set; }

    /// <summary>Gets or sets total PvP defense.</summary>
    public int DefensePvp { get; set; }

    /// <summary>Gets or sets PvM defense rate.</summary>
    public int DefenseRatePvm { get; set; }

    /// <summary>Gets or sets PvP defense rate.</summary>
    public int DefenseRatePvp { get; set; }

    /// <summary>Gets or sets PvM attack success rate.</summary>
    public int AttackRatePvm { get; set; }

    /// <summary>Gets or sets PvP attack success rate.</summary>
    public int AttackRatePvp { get; set; }

    /// <summary>Gets or sets physical attack speed.</summary>
    public int AttackSpeed { get; set; }

    /// <summary>Gets or sets magic casting speed.</summary>
    public int MagicSpeed { get; set; }

    /// <summary>Gets or sets maximum HP.</summary>
    public int MaximumHealth { get; set; }

    /// <summary>Gets or sets maximum Mana.</summary>
    public int MaximumMana { get; set; }

    /// <summary>Gets or sets maximum SD (Shield).</summary>
    public int MaximumShield { get; set; }

    /// <summary>Gets or sets maximum AG (Ability).</summary>
    public int MaximumAbility { get; set; }

    /// <summary>Gets or sets critical damage probability %.</summary>
    public double CriticalDamageChancePct { get; set; }

    /// <summary>Gets or sets excellent damage probability %.</summary>
    public double ExcellentDamageChancePct { get; set; }
}
