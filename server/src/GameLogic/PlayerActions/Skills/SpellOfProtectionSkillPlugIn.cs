// <copyright file="SpellOfProtectionSkillPlugIn.cs" company="HNMO">
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
/// The Season 9 skill Spell of Protection (618): grants a temporary damage-reduction barrier
/// (<see cref="SeasonNineSystemsConfiguration.SpellOfProtectionDefensePct"/>) when Season 9 systems and skills are active.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.SpellOfProtectionSkillPlugIn_Name), Description = nameof(PlugInResources.SpellOfProtectionSkillPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5E8F3A21-7C64-4B0D-9A52-E1C7B3D90618")]
public class SpellOfProtectionSkillPlugIn : TargetedSkillPluginBase
{
    /// <summary>The skill number of Spell of Protection.</summary>
    public const short SkillNumber = 618;

    private const short ProtectionEffectNumber = 0x22;
    private const float DurationSeconds = 15f;

    /// <inheritdoc />
    public override short Key => SkillNumber;

    /// <inheritdoc />
    public override async ValueTask PerformSkillAsync(Player player, IAttackable target, ushort skillId)
    {
        if (SeasonNineSystemsPlugIn.Get(player.GameContext) is not { } systems
            || !systems.IsActive()
            || systems.Configuration is not { SeasonNineSkillsEnabled: true } configuration)
        {
            return;
        }

        if (AttackActionValidator.IsIncapacitated(player)
            || player.SkillList?.GetSkill(skillId) is not { Skill: { } skill } skillEntry
            || !await AttackActionValidator.CanAttackAsync(player, skill).ConfigureAwait(false))
        {
            return;
        }

        if (!await player.TryConsumeForSkillAsync(skillEntry).ConfigureAwait(false))
        {
            return;
        }

        var pct = Math.Clamp(configuration.SpellOfProtectionDefensePct, 1, 100);
        var multiplier = Math.Max(0.01f, 1f - (pct / 100f));
        var effectDefinition = player.GameContext.Configuration.MagicEffects.FirstOrDefault(e => e.Number == ProtectionEffectNumber)
            ?? CreateRuntimeDefinition();

        if (player.Attributes is { } attributes)
        {
            var elements = effectDefinition.PowerUpDefinitions is { Count: > 0 } powerUps
                ? powerUps
                    .Select(p => new MagicEffect.ElementWithTarget(attributes.CreateElement(p), p.TargetAttribute!))
                    .ToArray()
                : [new MagicEffect.ElementWithTarget(new SimpleElement(multiplier, AggregateType.Multiplicate), Stats.DamageReceiveDecrement)];

            var duration = effectDefinition.Duration?.ConstantValue is { Value: > 0 } c ? c.Value : DurationSeconds;
            var magicEffect = new MagicEffect(TimeSpan.FromSeconds(duration), effectDefinition, elements);
            await player.MagicEffectList.AddEffectAsync(magicEffect).ConfigureAwait(false);
        }

        await player.ForEachWorldObserverAsync<IShowSkillAnimationPlugIn>(p => p.ShowSkillAnimationAsync(player, player, skill, true), true).ConfigureAwait(false);
    }

    private static MagicEffectDefinition CreateRuntimeDefinition()
        => new()
        {
            Number = ProtectionEffectNumber,
            Name = "Spell of Protection",
            InformObservers = true,
            StopByDeath = true,
        };
}
