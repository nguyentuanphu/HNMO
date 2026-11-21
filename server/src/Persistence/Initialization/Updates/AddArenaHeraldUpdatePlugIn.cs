// <copyright file="AddArenaHeraldUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.PlugIns;

/// <summary>
/// Adds the HNMO Arena Herald to existing saves: an NPC in Lorencia, Devias, Noria and Elbeland which opens the Arena window.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddArenaHeraldUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AddArenaHeraldUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("A3E58C21-7D46-4F9B-8E15-2C6B9F0D4A71")]
public class AddArenaHeraldUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => Properties.PlugInResources.AddArenaHeraldUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => Properties.PlugInResources.AddArenaHeraldUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 11, 30, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new HnmoArenaHeraldInitializer(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
