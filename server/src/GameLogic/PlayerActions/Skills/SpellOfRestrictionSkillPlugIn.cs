// <copyright file="SpellOfRestrictionSkillPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Skills;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.GameLogic.Views.World;
using HNMO.PlugIns;

/// <summary>
/// The Season 9 skill Spell of Restriction (619): binds a target in place temporarily when Season 9 systems and skills are active.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SpellOfRestrictionSkillPlugIn_Name), Description = nameof(PlugInResources.SpellOfRestrictionSkillPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5E8F3A21-7C64-4B0D-9A52-E1C7B3D90619")]
public class SpellOfRestrictionSkillPlugIn : TargetedSkillPluginBase
{
    /// <summary>The skill number of Spell of Restriction.</summary>
    public const short SkillNumber = 619;

    private const short RestrictionEffectNumber = 0x41;
    private const float DurationSeconds = 15f;

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
            || player.SkillList?.GetSkill(skillId) is not { Skill: { } skill } skillEntry
            || player.IsAtSafezone()
            || !target.IsActive()
            || !player.IsInRange(target.Position, skill.Range + 2)
            || (target is Player victim && !victim.MayBeHitBy(player))
            || !await AttackActionValidator.CanAttackAsync(player, skill).ConfigureAwait(false))
        {
            return;
        }

        if (!await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false))
        {
            return;
        }

        var effectDefinition = player.GameContext.Configuration.MagicEffects.FirstOrDefault(e => e.Number == RestrictionEffectNumber)
            ?? CreateRuntimeDefinition();

        var elements = effectDefinition.PowerUpDefinitions is { Count: > 0 } powerUps
            ? powerUps
                .Select(p => new MagicEffect.ElementWithTarget(target.Attributes.CreateElement(p), p.TargetAttribute!))
                .ToArray()
            : [new MagicEffect.ElementWithTarget(new SimpleElement(1f, AggregateType.AddFinal), Stats.IsFrozen)];

        var duration = effectDefinition.Duration?.ConstantValue is { Value: > 0 } c ? c.Value : DurationSeconds;
        var magicEffect = new MagicEffect(TimeSpan.FromSeconds(duration), effectDefinition, elements);
        await target.MagicEffectList.AddEffectAsync(magicEffect).ConfigureAwait(false);
        await player.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowSkillAnimationAsync(player, target, skill, true), true).ConfigureAwait(false);
    }

    private static MagicEffectDefinition CreateRuntimeDefinition()
        => new()
        {
            Number = RestrictionEffectNumber,
            Name = "Spell of Restriction",
            InformObservers = true,
            StopByDeath = true,
        };
}
