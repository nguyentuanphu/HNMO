// <copyright file="SkillEnergyRequirementsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// Updates the energy requirement of Recovery from 37 to 168 (matching the Season 6 and Season 9 server suites
/// and the Crystal of Recovery) and adds the missing 100 energy requirement to Power Slash.
/// </summary>
[PlugIn]
[Display(
    Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SkillEnergyRequirementsUpdatePlugIn_Name),
    Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.SkillEnergyRequirementsUpdatePlugIn_Description),
    ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("F4E1D08C-C861-4B74-A591-1ED5A4FD69E0")]
public class SkillEnergyRequirementsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => "Fix Recovery and Power Slash energy requirements";

    /// <inheritdoc />
    public override string Description => "Sets the energy requirement of Recovery to 168 (matching the Season 6 and Season 9 server suites and the Crystal of Recovery) and of Power Slash to 100.";

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.Recovery) is { } recovery)
        {
            var energy = FindRequirement(recovery, Stats.TotalEnergy);
            if (energy is { MinimumValue: 37 })
            {
                energy.MinimumValue = 168;
            }
        }

        if (gameConfiguration.Skills.FirstOrDefault(s => s.Number == (short)SkillNumber.PowerSlash) is { } powerSlash
            && FindRequirement(powerSlash, Stats.TotalEnergy) is null)
        {
            var requirement = context.CreateNew<AttributeRequirement>();
            requirement.Attribute = Stats.TotalEnergy.GetPersistent(gameConfiguration);
            requirement.MinimumValue = 100;
            powerSlash.Requirements.Add(requirement);
        }

        return ValueTask.CompletedTask;
    }

    private static AttributeRequirement? FindRequirement(Skill skill, AttributeDefinition stat)
    {
        return skill.Requirements.FirstOrDefault(r => r.Attribute == stat);
    }
}
