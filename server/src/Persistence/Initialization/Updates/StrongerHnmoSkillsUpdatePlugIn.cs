// <copyright file="StrongerHnmoSkillsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// This update makes the 28 HNMO skills (700 to 727) 20 % stronger: their damage and the strength of their buffs and
/// debuffs. Costs and cooldowns stay as they are. The trial quests of the Elf Soldier ask for 200 kills and five
/// Jewels of Chaos instead of 100 kills and one jewel.
/// </summary>
/// <remarks>
/// It sets the <see cref="HnmoSkillValues"/> in place on the skills, effects and quests which the
/// <see cref="AddHnmoSkillsUpdatePlugIn"/> added; setting them again changes nothing.
/// New databases get the same values from the <see cref="HnmoSkillsInitializer"/> and the <see cref="HnmoTrialQuestsInitializer"/>.
/// </remarks>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.StrongerHnmoSkillsUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.StrongerHnmoSkillsUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("A3C61F0E-94B2-4D78-8E15-2B7D9F4C6A83")]
public class StrongerHnmoSkillsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => Properties.PlugInResources.StrongerHnmoSkillsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => Properties.PlugInResources.StrongerHnmoSkillsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 14, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(AddHnmoSkillsUpdatePlugIn).GUID];

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        HnmoSkillValues.Apply(gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
