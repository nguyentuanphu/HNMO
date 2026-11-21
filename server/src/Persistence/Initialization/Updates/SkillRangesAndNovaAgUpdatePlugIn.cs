// <copyright file="SkillRangesAndNovaAgUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Aligns area-skill distances (Evil Spirit 6, Cometfall 6, Hellfire 0, Inferno 0 with EffectRange 4)
/// and ability (AG) consumptions (Nova 45 on skill 40, Chain Drive Strengthener 20 on skill 558)
/// with the Season 6/9 server suites while preserving each skill's area hit radius and HNMO's class lists.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("B9E41D72-3A58-4C19-8E2F-6D0A7B35C914")]
public class SkillRangesAndNovaAgUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Skill distances and Nova / Chain Drive Strengthener AG";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Aligns Evil Spirit, Cometfall, Hellfire and Inferno distances and Nova / Chain Drive Strengthener AG with the Season 6/9 server suites.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 19, 15, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        UpdateSkillRange(gameConfiguration, SkillNumber.EvilSpirit, oldRange: 7, newRange: 6);
        UpdateSkillRange(gameConfiguration, SkillNumber.Cometfall, oldRange: 3, newRange: 6);
        UpdateSkillRange(gameConfiguration, SkillNumber.BlastStrengthener, oldRange: 3, newRange: 6);
        UpdateSkillRange(gameConfiguration, SkillNumber.BlastStrengthenerDuelMaster, oldRange: 3, newRange: 6);

        UpdateSelfCenteredSkill(context, gameConfiguration, SkillNumber.Hellfire, oldRange: 4, effectRange: 2);
        UpdateSelfCenteredSkill(context, gameConfiguration, SkillNumber.HellfireStrengthener, oldRange: 4, effectRange: 2);
        UpdateSelfCenteredSkill(context, gameConfiguration, SkillNumber.Inferno, oldRange: 4, effectRange: 4);
        EnsureAreaEffectRange(context, gameConfiguration, SkillNumber.InfernoStrengthener, effectRange: 4);
        EnsureAreaEffectRange(context, gameConfiguration, SkillNumber.InfernoStrengthenerDuelMaster, effectRange: 4);

        UpdateAgRequirement(context, gameConfiguration, SkillNumber.Nova, oldAg: 0, newAg: 45);
        UpdateAgRequirement(context, gameConfiguration, SkillNumber.NovaStart, oldAg: 45, newAg: 0);
        UpdateAgRequirement(context, gameConfiguration, SkillNumber.ChainDriveStrengthener, oldAg: 22, newAg: 20);

        return ValueTask.CompletedTask;
    }

    private static void UpdateSkillRange(GameConfiguration config, SkillNumber number, short oldRange, short newRange)
    {
        if (config.Skills.FirstOrDefault(s => s.Number == (short)number) is { } skill
            && skill.Range == oldRange)
        {
            skill.Range = newRange;
        }
    }

    private static void UpdateSelfCenteredSkill(IContext context, GameConfiguration config, SkillNumber number, short oldRange, int effectRange)
    {
        if (config.Skills.FirstOrDefault(s => s.Number == (short)number) is not { } skill)
        {
            return;
        }

        if (skill.Range == oldRange)
        {
            skill.Range = 0;
        }

        EnsureAreaEffectRange(context, config, number, effectRange);
    }

    private static void EnsureAreaEffectRange(IContext context, GameConfiguration config, SkillNumber number, int effectRange)
    {
        if (config.Skills.FirstOrDefault(s => s.Number == (short)number) is not { } skill)
        {
            return;
        }

        if (skill.AreaSkillSettings is null)
        {
            var settings = context.CreateNew<AreaSkillSettings>();
            settings.EffectRange = effectRange;
            skill.AreaSkillSettings = settings;
        }
        else if (skill.AreaSkillSettings.EffectRange == 0)
        {
            skill.AreaSkillSettings.EffectRange = effectRange;
        }
    }

    private static void UpdateAgRequirement(IContext context, GameConfiguration config, SkillNumber number, int oldAg, int newAg)
    {
        if (config.Skills.FirstOrDefault(s => s.Number == (short)number) is not { } skill)
        {
            return;
        }

        var existing = skill.ConsumeRequirements.FirstOrDefault(r => r.Attribute == Stats.CurrentAbility);
        var currentAg = existing?.MinimumValue ?? 0;
        if (currentAg != oldAg)
        {
            return;
        }

        if (newAg == 0)
        {
            if (existing is not null)
            {
                skill.ConsumeRequirements.Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            existing = context.CreateNew<AttributeRequirement>();
            existing.Attribute = Stats.CurrentAbility.GetPersistent(config);
            skill.ConsumeRequirements.Add(existing);
        }

        existing.MinimumValue = newAg;
    }
}
