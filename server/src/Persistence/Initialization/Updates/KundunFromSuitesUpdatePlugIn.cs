// <copyright file="KundunFromSuitesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// This update gives Kundun (275, Kalima 7) the values of the Season 6/9 server suites: level 147, 5,000,000 health,
/// 2000-2500 damage, its defense, rates, resistances, attack range 15, view range 10, attack speed 2000 ms and 12 hours to respawn.
/// A value is only changed while it is still HNMO's old value.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("F2D8C9D9-DB78-4F69-9A7A-1F3E8D9BD2D5")]
public class KundunFromSuitesUpdatePlugIn : MonsterValuesUpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Kundun as in the suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Kundun (Kalima 7): 5,000,000 health and the other values of the Season 6/9 server suites.";

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 10, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IReadOnlyList<MonsterValueChange> Changes => ChangeList;

    /// <summary>
    /// Gets the changes: monster number, value, the value HNMO had, the value of the suites.
    /// </summary>
    internal static IReadOnlyList<MonsterValueChange> ChangeList { get; } =
    [
        new(275, MonsterValue.Level, 140, 147), // Illusion of Kundun 7
        new(275, MonsterValue.MaximumHealth, 140000, 5000000),
        new(275, MonsterValue.MinimumDamage, 1070, 2000),
        new(275, MonsterValue.MaximumDamage, 1105, 2500),
        new(275, MonsterValue.Defense, 892, 1500),
        new(275, MonsterValue.AttackRate, 1200, 2000),
        new(275, MonsterValue.DefenseRate, 450, 1000),
        new(275, MonsterValue.IceResistance, 60, 100),
        new(275, MonsterValue.PoisonResistance, 60, 254),
        new(275, MonsterValue.LightningResistance, 60, 100),
        new(275, MonsterValue.FireResistance, 60, 100),
        new(275, MonsterValue.AttackRange, 10, 15),
        new(275, MonsterValue.ViewRange, 7, 10),
        new(275, MonsterValue.AttackDelay, 1700, 2000),
        new(275, MonsterValue.RespawnDelay, 10800, 43200),
    ];
}
