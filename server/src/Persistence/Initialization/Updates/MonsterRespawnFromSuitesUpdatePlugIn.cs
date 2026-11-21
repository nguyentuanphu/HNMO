// <copyright file="MonsterRespawnFromSuitesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update gives the monsters the respawn time of the Season 6/9 server suites.
/// A value is only changed while it is still HNMO's old value.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("543AA176-207D-4451-BB32-25A066B98619")]
public class MonsterRespawnFromSuitesUpdatePlugIn : MonsterValuesUpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Monster respawn times as in the suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The time until a killed monster comes back, as in the Season 6/9 server suites.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 40, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IReadOnlyList<MonsterValueChange> Changes => ChangeList;

    /// <summary>
    /// Gets the changes: monster number, value, the value HNMO had, the value of the suites.
    /// </summary>
    internal static IReadOnlyList<MonsterValueChange> ChangeList { get; } =
    [
        new(0, MonsterValue.RespawnDelay, 3, 10), // Bull Fighter
        new(2, MonsterValue.RespawnDelay, 3, 10), // Budge Dragon
        new(18, MonsterValue.RespawnDelay, 10, 150), // Gorgon
        new(19, MonsterValue.RespawnDelay, 3, 10), // Yeti
        new(20, MonsterValue.RespawnDelay, 3, 10), // Elite Yeti
        new(53, MonsterValue.RespawnDelay, 600, 20), // Golden Titan
        new(54, MonsterValue.RespawnDelay, 600, 20), // Golden Soldier
        new(71, MonsterValue.RespawnDelay, 3, 10), // Mega Crust
        new(72, MonsterValue.RespawnDelay, 3, 30), // Phantom Knight
        new(74, MonsterValue.RespawnDelay, 3, 10), // Alpha Crust
        new(75, MonsterValue.RespawnDelay, 3, 30), // Great Drakan
        new(77, MonsterValue.RespawnDelay, 15, 150), // Dark Phoenix
        new(79, MonsterValue.RespawnDelay, 600, 5), // Golden Dragon
        new(80, MonsterValue.RespawnDelay, 600, 15), // Golden Lizard King
        new(81, MonsterValue.RespawnDelay, 600, 8), // Golden Vepar
        new(82, MonsterValue.RespawnDelay, 600, 70), // Golden Tantallos
        new(83, MonsterValue.RespawnDelay, 600, 70), // Golden Wheel
        new(361, MonsterValue.RespawnDelay, 0, 10), // Nightmare
        new(362, MonsterValue.RespawnDelay, 0, 10), // Maya (Hand Left)
        new(363, MonsterValue.RespawnDelay, 0, 10), // Maya (Hand Right)
        new(364, MonsterValue.RespawnDelay, 0, 10), // Maya
    ];
}
