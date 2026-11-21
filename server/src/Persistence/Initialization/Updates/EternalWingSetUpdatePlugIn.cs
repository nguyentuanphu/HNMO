// <copyright file="EternalWingSetUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>
/// This update turns the summoner set 44 into the Eternal Wing set of the Season 6/9 server suites and of the
/// clients' item data (owner's choice, 21 Nov 2026). HNMO had the stats of an older "Lilium" set: no level
/// requirement, more defense, other drop levels and requirements, and 6 attack speed on the gloves.
/// Only items which still have the old drop level are changed.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("1B5E8D24-6F93-4A07-B2C1-E9D07A3F5C86")]
public class EternalWingSetUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The plug-in name.</summary>
    internal const string PlugInName = "Eternal Wing set (summoner set 44) of the Season 6/9 server suites";

    /// <summary>The plug-in description.</summary>
    internal const string PlugInDescription = "The summoner set 44 gets the Eternal Wing name and stats: level 380, strength 62, agility 20, energy 170.";

    /// <summary>The durability of all pieces.</summary>
    internal const byte Durability = 86;

    /// <summary>Gets the group, the old and new drop level, the new defense and the new durability of the five pieces.</summary>
    internal static IReadOnlyList<(byte Group, byte OldDropLevel, byte DropLevel, int Defense)> Pieces { get; } =
    [
        (7, 110, 116, 35),
        (8, 113, 131, 49),
        (9, 102, 122, 41),
        (10, 82, 102, 29),
        (11, 90, 99, 32),
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
    public override DateTime CreatedAt => new(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var (group, oldDropLevel, dropLevel, defense) in Pieces)
        {
            if (gameConfiguration.Items.FirstOrDefault(i => i.Group == group && i.Number == 44) is not { } item
                || item.DropLevel != oldDropLevel)
            {
                continue;
            }

            item.Name = item.Name.GetFromSource() ?? item.Name;
            foreach (var set in gameConfiguration.ItemSetGroups.Where(s => s.AlwaysApplies && s.Items.Any(i => i.ItemDefinition == item)))
            {
                set.Name = set.Name.GetFromSource() ?? set.Name;
            }

            item.DropLevel = dropLevel;
            item.Durability = Durability;
            if (item.BasePowerUpAttributes.FirstOrDefault(p => p.TargetAttribute == Stats.DefenseBase) is { } defensePowerUp)
            {
                defensePowerUp.BaseValue = defense;
            }

            if (group == 10 && item.BasePowerUpAttributes.FirstOrDefault(p => p.TargetAttribute == Stats.AttackSpeedAny) is { } speed)
            {
                item.BasePowerUpAttributes.Remove(speed);
            }

            SetRequirement(Stats.Level, 380);
            SetRequirement(Stats.TotalStrengthRequirementValue, 62);
            SetRequirement(Stats.TotalAgilityRequirementValue, 20);
            SetRequirement(Stats.TotalEnergyRequirementValue, 170);

            void SetRequirement(AttributeDefinition attribute, int value)
            {
                if (item.Requirements.FirstOrDefault(r => r.Attribute == attribute) is { } requirement)
                {
                    requirement.MinimumValue = value;
                    return;
                }

                requirement = context.CreateNew<AttributeRequirement>();
                requirement.Attribute = attribute.GetPersistent(gameConfiguration);
                requirement.MinimumValue = value;
                item.Requirements.Add(requirement);
            }
        }

        return ValueTask.CompletedTask;
    }
}
