// <copyright file="BloodCastleDurationUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update sets the play time of the Blood Castles to 15 minutes, as in the Season 6/9 server suites
/// (Blood Castle time settings: open 5 minutes, play 15 minutes, close 1 minute after the weapon is delivered).
/// It was 20 minutes. A play time the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("2F9B6D84-1E3A-4C75-9D20-B8E54A17C3F9")]
public class BloodCastleDurationUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Blood Castle play time of the Season 6/9 server suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Blood Castle lasts 15 minutes (was 20), unless the host changed it.";

    /// <summary>The play time of the suites.</summary>
    internal static readonly TimeSpan NewDuration = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan OldDuration = TimeSpan.FromMinutes(20);

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 15, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var bloodCastle in gameConfiguration.MiniGameDefinitions.Where(d => d.Type == MiniGameType.BloodCastle && d.GameDuration == OldDuration))
        {
            bloodCastle.GameDuration = NewDuration;
        }

        return ValueTask.CompletedTask;
    }
}
