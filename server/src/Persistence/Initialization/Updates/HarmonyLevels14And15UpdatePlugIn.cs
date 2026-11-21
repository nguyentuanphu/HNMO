// <copyright file="HarmonyLevels14And15UpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Attributes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.PlugIns;

/// <summary>
/// This update adds the jewel of harmony option values for items +14 and +15, as in the harmony option table of the
/// Season 6/9 server suites (owner's choice, 21 Nov 2026). The options ended at +13, so a +14 or +15 item couldn't be
/// refined further and kept its +13 value. An option which already has these levels stays.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("F48B2E06-91D7-4A3C-B5E8-2D6C0F7A1934")]
public class HarmonyLevels14And15UpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Jewel of harmony values for items +14 and +15 (Season 6/9 server suites)";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Adds the harmony option values of the levels 14 and 15 which the Season 6/9 server suites have.";

    /// <summary>
    /// Gets the values of the levels 14 and 15 by the parent number of the option (<see cref="ItemOptionDefinitionNumbers"/>)
    /// and option number.
    /// </summary>
    internal static IReadOnlyList<(short Definition, int Option, float Level14, float Level15)> Values { get; } = CreateValues();

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 16, 40, 0, DateTimeKind.Utc);

    /// <summary>Adds the levels 14 and 15 to the harmony options which end at level 13.</summary>
    /// <param name="context">The context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    internal static void AddLevels(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (definitionNumber, optionNumber, level14, level15) in Values)
        {
            var id = GuidHelper.CreateGuid<IncreasableItemOption>(definitionNumber, (short)optionNumber);
            var option = gameConfiguration.ItemOptions.SelectMany(d => d.PossibleOptions).FirstOrDefault(o => o.GetId() == id);
            var levelThirteen = option?.LevelDependentOptions.FirstOrDefault(l => l.Level == 13);
            if (option is null || levelThirteen?.PowerUpDefinition?.Boost is not { } boost)
            {
                continue;
            }

            foreach (var (level, value) in new[] { (14, level14), (15, level15) })
            {
                if (option.LevelDependentOptions.Any(l => l.Level == level))
                {
                    continue;
                }

                var optionOfLevel = context.CreateNew<ItemOptionOfLevel>();
                optionOfLevel.Level = level;
                optionOfLevel.RequiredItemLevel = level;
                var powerUp = context.CreateNew<PowerUpDefinition>();
                powerUp.TargetAttribute = levelThirteen.PowerUpDefinition.TargetAttribute;
                powerUp.Boost = context.CreateNew<PowerUpDefinitionValue>();
                powerUp.Boost.ConstantValue.Value = value;
                powerUp.Boost.ConstantValue.AggregateType = boost.ConstantValue.AggregateType;
                optionOfLevel.PowerUpDefinition = powerUp;
                option.LevelDependentOptions.Add(optionOfLevel);
            }
        }
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        AddLevels(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private static List<(short Definition, int Option, float Level14, float Level15)> CreateValues()
    {
        var physical = ItemOptionDefinitionNumbers.HarmonyPhysical;
        var defense = ItemOptionDefinitionNumbers.HarmonyDefense;
        var result = new List<(short Definition, int Option, float Level14, float Level15)>
        {
            // Weapons: minimum / maximum damage, strength / agility requirement, damage, critical damage, skill damage,
            // PvP attack rate, SD decrease, SD ignore chance.
            (physical, 1, 23, 26), (physical, 2, 32, 35), (physical, 3, 43, 46), (physical, 4, 43, 46),
            (physical, 5, 22, 25), (physical, 6, 33, 36), (physical, 7, 24, 26), (physical, 8, 16, 18),
            (physical, 9, 11, 12), (physical, 10, 0.12f, 0.14f),

            // Armors: defense, maximum AG, maximum life, life recovery, mana recovery, PvP defense rate, damage decrease, SD rate.
            (defense, 1, 28, 31), (defense, 2, 28, 31), (defense, 3, 32, 34), (defense, 4, 9, 10),
            (defense, 5, 6, 7), (defense, 6, 10, 12), (defense, 7, 0.08f, 0.09f), (defense, 8, 0.06f, 0.07f),
        };

        // Staffs and the summoner's books share the staff values: wizardry damage, strength / agility requirement,
        // skill damage, critical damage, SD decrease, PvP attack rate, SD ignore chance.
        foreach (var staff in new[] { ItemOptionDefinitionNumbers.HarmonyWizardry, ItemOptionDefinitionNumbers.HarmonyCurse })
        {
            result.AddRange(
            [
                (staff, 1, 33, 35), (staff, 2, 43, 46), (staff, 3, 43, 46), (staff, 4, 33, 36),
                (staff, 5, 30, 32), (staff, 6, 15, 17), (staff, 7, 15, 16), (staff, 8, 0.17f, 0.19f),
            ]);
        }

        return result;
    }
}
