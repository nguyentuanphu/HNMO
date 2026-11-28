// <copyright file="SpellOfPursuitSkillPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.GameLogic.Views.World;
using HNMO.PlugIns;

/// <summary>
/// The Season 9 skill Spell of Pursuit (620): warps the caster next to the target when Season 9 systems and skills are active.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SpellOfPursuitSkillPlugIn_Name), Description = nameof(PlugInResources.SpellOfPursuitSkillPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5E8F3A21-7C64-4B0D-9A52-E1C7B3D90620")]
public class SpellOfPursuitSkillPlugIn : TargetedSkillPluginBase
{
    /// <summary>The skill number of Spell of Pursuit.</summary>
    public const short SkillNumber = 620;

    /// <inheritdoc />
    public override short Key => SkillNumber;

    /// <inheritdoc />
    public override async ValueTask PerformSkillAsync(Player player, IAttackable target, ushort skillId)
    {
        if (SeasonNineSystemsPlugIn.Get(player.GameContext) is not { } systems
            || !systems.IsActive()
            || systems.Configuration is not { SeasonNineSkillsEnabled: true })
        {
            return;
        }

        if (target is null
            || target == player
            || AttackActionValidator.IsIncapacitated(player)
            || player.Attributes is null
            || player.Attributes[Stats.IsStunned] > 0
            || player.Attributes[Stats.IsFrozen] > 0
            || player.SkillList?.GetSkill(skillId) is not { Skill: { } skill } skillEntry
            || player.IsAtSafezone()
            || !target.IsActive()
            || !ReferenceEquals(player.CurrentMap, target.CurrentMap)
            || (target is Player victim && (!victim.MayBeHitBy(player) || victim.IsAtSafezone()))
            || !await AttackActionValidator.CanAttackAsync(player, skill).ConfigureAwait(false))
        {
            return;
        }

        if (!await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false))
        {
            return;
        }

        await player.MoveAsync(target.Position).ConfigureAwait(false);
        await player.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowSkillAnimationAsync(player, target, skill, true), true).ConfigureAwait(false);
    }
}
