// <copyright file="FixElvenlandMonstersUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// This update gives the Elvenland monsters (418-425) the values of the Season 6/9 server suites.
/// The monsters 419-421 had each other's names and stats (419 is the Hideous Rabbit, 420 the Werewolf,
/// 421 the Polluted Butterfly, as in both clients), and all eight had placeholder values: an attack rate
/// of 10, the defense as defense rate, 1500 ms attack speed and view range 5. The Cursed Lich cast its
/// Meteorite from melee range.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("A3E7C1D9-5B2F-4E86-9C14-7D0B3F6A8E25")]
public class FixElvenlandMonstersUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Elvenland monsters as in the suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Elvenland monsters 418-425: the right names and stats for 419-421 and the attack rate, ranges and speeds of the Season 6/9 server suites.";

    /// <summary>
    /// Gets the values of the Season 6/9 server suites, by monster number.
    /// </summary>
    internal static IReadOnlyList<ElvenlandMonster> Monsters { get; } =
    [
        new(418, null, 4, 60, 10, 13, 3, 18, 3, 3, 1, 4, 2000, 10),
        new(419, () => MonsterNames.HideousRabbit, 19, 520, 68, 72, 22, 93, 22, 3, 1, 4, 1400, 10),
        new(420, () => MonsterNames.Werewolf421, 24, 720, 85, 90, 30, 120, 30, 3, 1, 5, 1800, 10),
        new(421, () => MonsterNames.PollutedButterfly, 13, 230, 37, 42, 13, 58, 13, 3, 1, 4, 1600, 10),
        new(422, null, 30, 900, 105, 110, 33, 150, 36, 3, 4, 7, 2000, 10),
        new(423, null, 36, 1200, 120, 125, 50, 180, 43, 3, 1, 6, 1400, 10),
        new(424, null, 43, 2400, 130, 140, 65, 215, 52, 2, 2, 6, 1600, 8),
        new(425, null, 48, 3000, 150, 155, 80, 240, 70, 3, 1, 5, 1400, 10),
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
    public override DateTime CreatedAt => new(2026, 11, 21, 15, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var values in Monsters)
        {
            if (gameConfiguration.Monsters.FirstOrDefault(m => m.Number == values.Number) is not { } monster)
            {
                continue;
            }

            if (values.Name is { } name)
            {
                monster.Designation = LocalizedString.FromResource(name);
            }

            monster.MoveRange = values.MoveRange;
            monster.AttackRange = values.AttackRange;
            monster.ViewRange = values.ViewRange;
            monster.AttackDelay = TimeSpan.FromMilliseconds(values.AttackDelayMilliseconds);
            monster.RespawnDelay = TimeSpan.FromSeconds(values.RespawnSeconds);
            Set(monster, Stats.Level, values.Level);
            Set(monster, Stats.MaximumHealth, values.Health);
            Set(monster, Stats.MinimumPhysBaseDmg, values.MinimumDamage);
            Set(monster, Stats.MaximumPhysBaseDmg, values.MaximumDamage);
            Set(monster, Stats.DefenseBase, values.Defense);
            Set(monster, Stats.AttackRatePvm, values.AttackRate);
            Set(monster, Stats.DefenseRatePvm, values.DefenseRate);
        }

        return ValueTask.CompletedTask;
    }

    private static void Set(MonsterDefinition monster, HNMO.AttributeSystem.AttributeDefinition attribute, float value)
    {
        if (monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == attribute.Id) is { } monsterAttribute)
        {
            monsterAttribute.Value = value;
        }
    }

    /// <summary>
    /// The values of one Elvenland monster.
    /// </summary>
    internal sealed record ElvenlandMonster(
        short Number,
        System.Linq.Expressions.Expression<Func<string>>? Name,
        float Level,
        float Health,
        float MinimumDamage,
        float MaximumDamage,
        float Defense,
        float AttackRate,
        float DefenseRate,
        byte MoveRange,
        byte AttackRange,
        short ViewRange,
        int AttackDelayMilliseconds,
        int RespawnSeconds);
}
