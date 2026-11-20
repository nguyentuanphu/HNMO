// <copyright file="RenumberArenaHeraldUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.PlugIns;

/// <summary>
/// Moves the Arena Herald of a save made before it got its own number (595, a monster of the Season 9 data) to 900, so the
/// web game shows and talks to it again; adds it where it is missing.
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.RenumberArenaHeraldUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.RenumberArenaHeraldUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("5B2E9C47-1A83-4D6F-B0E2-7C94D1A35F18")]
public class RenumberArenaHeraldUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => Properties.PlugInResources.RenumberArenaHeraldUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => Properties.PlugInResources.RenumberArenaHeraldUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 7, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new HnmoArenaHeraldInitializer(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
