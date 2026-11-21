// <copyright file="FixMonsterStatTyposUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>
/// This update fixes five monster stats which had an extra zero: each was ten times the value of the
/// Season 6/9 server suites and out of line with the neighbouring monsters of the same event.
/// Only a value which is still the wrong one is changed, so a value the host chose stays.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("6F1C2B7E-3A54-4D0B-9E83-2C5A17D4B9E1")]
public class FixMonsterStatTyposUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Fix monster stats with an extra zero";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Chaos Castle, Kalima and Karutan monsters: five stats which were ten times too high.";

    /// <summary>
    /// Gets the fixes: monster number, attribute, wrong value, right value.
    /// </summary>
    internal static IReadOnlyList<(short Monster, HNMO.AttributeSystem.AttributeDefinition Attribute, float Wrong, float Right)> Fixes { get; } =
    [
        (168, Stats.MaximumHealth, 65000, 6500), // Chaos Castle monster 7
        (426, Stats.DefenseRatePvm, 1900, 190), // Chaos Castle monster 13
        (184, Stats.DefenseRatePvm, 600, 60), // Blood Soldier 3 (Kalima 3)
        (197, Stats.AttackRatePvm, 7800, 780), // Illusion of Kundun 4 (Kalima 4)
        (574, Stats.AttackRatePvm, 11400, 1140), // Crypos (Karutan)
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
        foreach (var (number, attribute, wrong, right) in Fixes)
        {
            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == number)?.Attributes
                    .FirstOrDefault(a => a.AttributeDefinition?.Id == attribute.Id) is { } value
                && Math.Abs(value.Value - wrong) < 0.5f)
            {
                value.Value = right;
            }
        }

        return ValueTask.CompletedTask;
    }
}
