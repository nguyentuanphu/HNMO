// <copyright file="BloodSlashSkillPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Handles the Blood Slash skill of the dark knight (HNMO skill 701): the hero is healed by a part of the damage it deals,
/// like the drain life skill of the summoner.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.BloodSlashSkillPlugIn_Name), Description = nameof(PlugInResources.BloodSlashSkillPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("B7D3E1A4-5C29-4F86-A0E2-6D4B9C17F358")]
public class BloodSlashSkillPlugIn : IAreaSkillPlugIn
{
    /// <summary>
    /// The part of the damage which heals the hero.
    /// </summary>
    /// <remarks>
    /// A fixed part of the skill as defined for HNMO (6 %), not a value a server owner should have to tune separately.
    /// </remarks>
    internal const float HealedPartOfDamage = 0.06f;

    /// <inheritdoc/>
    public short Key => 701;

    /// <inheritdoc/>
    public ValueTask AfterTargetGotAttackedAsync(IAttacker attacker, IAttackable target, SkillEntry skillEntry, Point targetAreaCenter, HitInfo? hitInfo)
    {
        if (attacker is not Player { Attributes: { } attributes }
            || hitInfo is not { } hit
            || hit.TotalDamage <= 0)
        {
            return ValueTask.CompletedTask;
        }

        // The hit info is the one of the first of the skill's hits; the others hit about as hard.
        var hits = Math.Max(1, (int)(skillEntry.Skill?.NumberOfHitsPerAttack ?? 1));
        var healing = hit.TotalDamage * hits * HealedPartOfDamage;
        attributes[Stats.CurrentHealth] = Math.Min(attributes[Stats.MaximumHealth], attributes[Stats.CurrentHealth] + healing);
        return ValueTask.CompletedTask;
    }
}
