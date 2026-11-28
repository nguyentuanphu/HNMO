// <copyright file="ShieldBurnSkillPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.GameLogic.Views.World;
using HNMO.PlugIns;

/// <summary>
/// The Season 9 skill Shield-Burn (621): burns a part of the target player's shield (SD,
/// <see cref="SeasonNineSystemsConfiguration.ShieldBurnSdPercent"/>) and leaves the health as it is.
/// </summary>
/// <remarks>
/// It casts only while the Season 9 systems run (a Season 9 realm, the systems and their skills switched on), at a
/// player the caster may hit by the rules of a normal hit; otherwise nothing is spent. Monsters have no shield, so it
/// doesn't cast at them.
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShieldBurnSkillPlugIn_Name), Description = nameof(PlugInResources.ShieldBurnSkillPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5E8F3A21-7C64-4B0D-9A52-E1C7B3D90621")]
public class ShieldBurnSkillPlugIn : TargetedSkillPluginBase
{
    /// <summary>The skill number of Shield-Burn.</summary>
    public const short ShieldBurnSkillNumber = 621;

    /// <inheritdoc />
    public override short Key => ShieldBurnSkillNumber;

    /// <inheritdoc />
    public override async ValueTask PerformSkillAsync(Player player, IAttackable target, ushort skillId)
    {
        if (SeasonNineSystemsPlugIn.Get(player.GameContext) is not { } systems
            || !systems.IsActive()
            || systems.Configuration is not { SeasonNineSkillsEnabled: true } configuration)
        {
            return;
        }

        if (target is not Player victim
            || victim == player
            || AttackActionValidator.IsIncapacitated(player)
            || player.SkillList?.GetSkill(skillId) is not { Skill: { } skill } skillEntry
            || player.IsAtSafezone()
            || !victim.IsActive()
            || !player.IsInRange(victim.Position, skill.Range + 2)
            || !victim.MayBeHitBy(player)
            || !await AttackActionValidator.CanAttackAsync(player, skill).ConfigureAwait(false))
        {
            return;
        }

        // Validated last, so a refused cast spends nothing (mana, cooldown).
        if (!await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false))
        {
            return;
        }

        var burned = await victim.BurnShieldAsync(player, configuration.ShieldBurnSdPercent).ConfigureAwait(false);
        await player.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowSkillAnimationAsync(player, victim, skill, burned > 0), true).ConfigureAwait(false);
    }
}
