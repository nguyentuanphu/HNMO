// <copyright file="GroundDropDurationUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update shortens the time drops stay on the ground as in the Season 9 server suites (owner's choice, 20 Nov 2026):
/// items 20 seconds instead of 60. A time the host changed is kept. Zen gets its own time of 15 seconds by the
/// database migration which adds it.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("B47E1D29-6A3C-4F85-8D10-2C9F5A7E3B64")]
public class GroundDropDurationUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Drops on the ground: items 20 s, zen 15 s";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Items stay 20 seconds on the ground instead of 60; zen 15 seconds.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.ItemDropDuration == TimeSpan.FromSeconds(60))
        {
            gameConfiguration.ItemDropDuration = TimeSpan.FromSeconds(20);
        }

        if (gameConfiguration.MoneyDropDuration <= TimeSpan.Zero)
        {
            gameConfiguration.MoneyDropDuration = TimeSpan.FromSeconds(15);
        }

        return ValueTask.CompletedTask;
    }
}
