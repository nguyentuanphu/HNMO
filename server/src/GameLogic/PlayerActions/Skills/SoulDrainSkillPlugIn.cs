// <copyright file="SoulDrainSkillPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Handles the Soul Drain skill of the summoner (HNMO skill 723): the hero gets mana for the damage it deals,
/// like the drain life skill gives health. A player target loses this mana; monsters have no mana to lose.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SoulDrainSkillPlugIn_Name), Description = nameof(PlugInResources.SoulDrainSkillPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3F1A9C62-8E47-4B05-9D13-C2E6A84B7F21")]
public class SoulDrainSkillPlugIn : IAreaSkillPlugIn
{
    /// <summary>
    /// The part of the damage which the hero gets as mana.
    /// </summary>
    /// <remarks>
    /// A fixed part of the skill as defined for HNMO (12 %), not a value a server owner should have to tune separately.
    /// </remarks>
    internal const float DrainedPartOfDamage = 0.12f;

    /// <inheritdoc/>
    public short Key => 723;

    /// <inheritdoc/>
    public ValueTask AfterTargetGotAttackedAsync(IAttacker attacker, IAttackable target, SkillEntry skillEntry, Point targetAreaCenter, HitInfo? hitInfo)
    {
        if (attacker is not Player { Attributes: { } attributes }
            || hitInfo is not { } hit
            || hit.TotalDamage <= 0)
        {
            return ValueTask.CompletedTask;
        }

        var mana = hit.TotalDamage * DrainedPartOfDamage;
        if (target is Player { Attributes: { } targetAttributes })
        {
            mana = Math.Min(mana, targetAttributes[Stats.CurrentMana]);
            targetAttributes[Stats.CurrentMana] -= mana;
        }

        attributes[Stats.CurrentMana] = Math.Min(attributes[Stats.MaximumMana], attributes[Stats.CurrentMana] + mana);
        return ValueTask.CompletedTask;
    }
}
