// <copyright file="RaklionSwampGateLevelsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.PlugIns;

/// <summary>
/// This update sets the level requirements of the Raklion gates (Devias to Raklion, inside Raklion and to the
/// boss area: 286, 288, 290, 292) to 270 and of the gates inside the Swamp of Peace (274 to 285) to 400, as in the
/// gate settings of the Season 6/9 server suites. They were 240 and 250. A level the host changed is kept.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A84E1C37-6F02-4B9D-8E5A-3C71D0F2B6E8")]
public class RaklionSwampGateLevelsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Raklion and Swamp of Peace gate levels of the Season 6/9 server suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Raklion gates need level 270 (was 240), the gates inside the Swamp of Peace level 400 (was 250), unless the host changed them.";

    /// <summary>Gets the gate number, the old and the new level requirement.</summary>
    internal static IReadOnlyList<(short Gate, short OldLevel, short NewLevel)> Changes { get; } =
    [
        (274, 250, 400),
        (276, 250, 400),
        (277, 250, 400),
        (279, 250, 400),
        (280, 250, 400),
        (282, 250, 400),
        (283, 250, 400),
        (285, 250, 400),
        (286, 240, 270),
        (288, 240, 270),
        (290, 240, 270),
        (292, 240, 270),
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 14, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var gate in gameConfiguration.Maps.SelectMany(m => m.EnterGates))
        {
            foreach (var (number, oldLevel, newLevel) in Changes)
            {
                if (gate.Number == number && gate.LevelRequirement == oldLevel)
                {
                    gate.LevelRequirement = newLevel;
                }
            }
        }

        return ValueTask.CompletedTask;
    }
}
