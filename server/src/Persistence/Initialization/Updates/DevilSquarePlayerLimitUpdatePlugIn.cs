// <copyright file="DevilSquarePlayerLimitUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update lets 15 players into every Devil Square (owner's choice, 2026-11-20, like the Season 6/9 server
/// suites). Only the earlier default of 10 is replaced; a limit the host changed stays as it is.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("B47E2D93-5A16-4C8F-9E31-0D6A8F2C71E5")]
public class DevilSquarePlayerLimitUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Devil Square: 15 players";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Lets 15 players into every Devil Square, unless the host changed the limit.";

    /// <summary>The earlier default player limit of a Devil Square.</summary>
    internal const int PreviousMaximumPlayerCount = 10;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var definition in gameConfiguration.MiniGameDefinitions.Where(d => d.Type == MiniGameType.DevilSquare && d.MaximumPlayerCount == PreviousMaximumPlayerCount))
        {
            definition.MaximumPlayerCount = VersionSeasonSix.Events.DevilSquareInitializer.MaximumPlayerCount;
        }

        return ValueTask.CompletedTask;
    }
}
