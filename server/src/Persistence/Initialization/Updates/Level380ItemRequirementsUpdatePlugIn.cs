// <copyright file="Level380ItemRequirementsUpdatePlugIn.cs" company="HNMO">
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
/// This update adds the requirements of the level 380 items which the Season 6/9 server suites have and HNMO lacked:
/// level 380 for the Titan, Brave, Phantom, Destroy, Seraphim, Faith, Paewang and Hades sets, Frost Mace,
/// Absolute Scepter, Stinger Bow and the four 380 shields; energy for the Destroy, Faith and Hades gloves (the other
/// pieces of these sets had it); vitality 85 for the Phoenix Soul set. A requirement which is already there stays.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("5A0C7E93-2B64-4D18-8F5E-91C3A6D7B240")]
public class Level380ItemRequirementsUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Requirements of the level 380 items (Season 6/9 server suites)";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "Level 380 for the 380 sets, weapons and shields; energy for the Destroy, Faith and Hades gloves; vitality 85 for the Phoenix Soul set.";

    /// <summary>
    /// Gets the requirements which the Season 6/9 server suites (item list) have for these items: group, number,
    /// level, energy and vitality (0 = no change).
    /// </summary>
    internal static IReadOnlyList<(byte Group, short Number, int Level, int Energy, int Vitality)> Requirements { get; } = CreateRequirements();

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 21, 11, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (group, number, level, energy, vitality) in Requirements)
        {
            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == number) is not { } item)
            {
                continue;
            }

            AddIfMissing(Stats.Level, level);
            AddIfMissing(Stats.TotalEnergyRequirementValue, energy);
            AddIfMissing(Stats.TotalVitalityRequirementValue, vitality);

            void AddIfMissing(AttributeDefinition attribute, int value)
            {
                if (value == 0 || item.Requirements.Any(r => r.Attribute == attribute))
                {
                    return;
                }

                var requirement = context.CreateNew<AttributeRequirement>();
                requirement.Attribute = attribute.GetPersistent(gameConfiguration);
                requirement.MinimumValue = value;
                item.Requirements.Add(requirement);
            }
        }

        return ValueTask.CompletedTask;
    }

    private static List<(byte Group, short Number, int Level, int Energy, int Vitality)> CreateRequirements()
    {
        var result = new List<(byte Group, short Number, int Level, int Energy, int Vitality)>
        {
            (2, 16, 380, 0, 0), // Frost Mace
            (2, 17, 380, 0, 0), // Absolute Scepter
            (4, 23, 380, 0, 0), // Stinger Bow
            (6, 17, 380, 0, 0), // Crimson Glory
            (6, 18, 380, 0, 0), // Salamander Shield
            (6, 19, 380, 0, 0), // Frost Barrier
            (6, 20, 380, 0, 0), // Guardian Shield
        };

        for (byte group = 7; group <= 11; group++)
        {
            for (short number = 45; number <= 52; number++)
            {
                // The Magic Gladiator sets (Phantom, Destroy) have no helm.
                if (group == 7 && number is 47 or 48)
                {
                    continue;
                }

                var energy = group == 10 ? number switch { 48 => 165, 50 => 138, 52 => 181, _ => 0 } : 0;
                result.Add((group, number, 380, energy, 0));
            }
        }

        foreach (byte group in new byte[] { 7, 8, 9, 11 })
        {
            result.Add((group, 73, 0, 0, 85)); // Phoenix Soul
        }

        return result;
    }
}
