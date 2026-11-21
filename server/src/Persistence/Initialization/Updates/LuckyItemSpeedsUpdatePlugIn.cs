// <copyright file="LuckyItemSpeedsUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.PlugIns;

/// <summary>
/// This update gives the lucky gloves (10, 62-71) their attack speed and the lucky boots (11, 62-72) their walk speed,
/// as in the item lists of the Season 6/9 server suites (owner's choice, 21 Nov 2026; the item flags stay as HNMO's).
/// A speed the item already has stays.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("6E1D9A37-C2F8-4B05-9374-A1B8E50D6F29")]
public class LuckyItemSpeedsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Speeds of the lucky gloves and boots (Season 6/9 server suites)";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The lucky gloves get their attack speed and the lucky boots their walk speed, unless they have one.";

    /// <summary>Gets the group, number and speed of the lucky gloves (attack speed) and boots (walk speed).</summary>
    internal static IReadOnlyList<(byte Group, short Number, int Speed)> Speeds { get; } =
    [
        (10, 62, 10), (10, 63, 8), (10, 65, 6), (10, 66, 6), (10, 67, 6), (10, 68, 6), (10, 70, 8), (10, 71, 6),
        (11, 62, 8), (11, 64, 8), (11, 67, 2), (11, 71, 2),
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
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 30, 0, DateTimeKind.Utc);

    /// <summary>Adds the speeds which the lucky gloves and boots don't have yet.</summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void AddMissingSpeeds(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (group, number, speed) in Speeds)
        {
            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is not { } item)
            {
                continue;
            }

            var attribute = group == 10 ? Stats.AttackSpeedAny : Stats.WalkSpeed;
            if (item.BasePowerUpAttributes.Any(p => p.TargetAttribute == attribute))
            {
                continue;
            }

            var powerUp = context.CreateNew<ItemBasePowerUpDefinition>();
            powerUp.TargetAttribute = attribute.GetPersistent(gameConfiguration);
            powerUp.BaseValue = speed;
            powerUp.AggregateType = AggregateType.AddRaw;
            item.BasePowerUpAttributes.Add(powerUp);
        }
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        AddMissingSpeeds(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
