// <copyright file="AddHnmoSkillsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.Skills;
using HNMO.PlugIns;

/// <summary>
/// This update adds the 28 HNMO skills (700 to 727) with their effects, and the trial quests of the Elf Soldier
/// which teach the D skill of each class.
/// </summary>
/// <remarks>
/// New databases get the same data from the <see cref="HnmoSkillsInitializer"/> and the
/// <see cref="HnmoTrialQuestsInitializer"/>, which this update calls; both do nothing when their data exists already.
/// </remarks>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddHnmoSkillsUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddHnmoSkillsUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("5E0C7B83-2A4F-4C1D-9B6E-8F3A1D27C940")]
public class AddHnmoSkillsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => Properties.PlugInResources.AddHnmoSkillsUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => Properties.PlugInResources.AddHnmoSkillsUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new HnmoSkillsInitializer(context, gameConfiguration).Initialize();
        new HnmoTrialQuestsInitializer(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
