// <copyright file="TalismanBagSummary.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

/// <summary>
/// Aggregated passive bonuses and granted magic effects computed from a character's carried bag talismans.
/// </summary>
/// <param name="ActiveCount">Number of active talisman stacks matched in the bag.</param>
/// <param name="BonusExpPercent">Summed bonus experience percent.</param>
/// <param name="BonusZenPercent">Summed bonus Zen percent.</param>
/// <param name="BonusDamage">Summed flat bonus damage.</param>
/// <param name="BonusDefense">Summed flat bonus defense.</param>
/// <param name="BonusMaxHp">Summed flat bonus max health.</param>
/// <param name="BonusAttackSpeed">Summed bonus attack speed.</param>
/// <param name="CriticalChancePercent">Summed bonus critical chance percent.</param>
/// <param name="GrantedEffectNumbers">Distinct magic effect numbers granted while carried.</param>
public sealed record TalismanBagSummary(
    int ActiveCount,
    float BonusExpPercent,
    float BonusZenPercent,
    int BonusDamage,
    int BonusDefense,
    int BonusMaxHp,
    int BonusAttackSpeed,
    float CriticalChancePercent,
    IReadOnlyList<short> GrantedEffectNumbers)
{
    /// <summary>Gets a value indicating whether any attribute bonus is non-zero.</summary>
    public bool HasAnyStatBonus =>
        this.BonusExpPercent > 0
        || this.BonusZenPercent > 0
        || this.BonusDamage > 0
        || this.BonusDefense > 0
        || this.BonusMaxHp > 0
        || this.BonusAttackSpeed > 0
        || this.CriticalChancePercent > 0;
}
