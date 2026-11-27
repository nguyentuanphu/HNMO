// <copyright file="AuditDataFixesUpdatePlugInSeason6.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Items;
using HNMO.Persistence.Initialization.Properties;
using HNMO.PlugIns;

/// <summary>
/// The 25 Nov 2026 audit fixes for Season 6 databases: Hanzo's store, the excellent damage chance of the Kantata set
/// (10.0, i.e. every hit excellent, instead of 0.10) and the SD of the Guardian option on boots (700, was 200).
/// </summary>
[PlugIn]
[Display(Name = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Name), Description = nameof(HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Description), ResourceType = typeof(HNMO.Persistence.Initialization.Properties.PlugInResources))]
[Guid("3E5B6C41-8A0D-4B7E-9C2F-6D1A4E8B7F21")]
public class AuditDataFixesUpdatePlugInSeason6 : AuditDataFixesUpdatePlugInBase
{
    private const float GuardianBootsShield = 700;

    /// <summary>The normal items of the Blue Chocolate Box (+0) and the Deep Blue Candy Box (+1), as in the initializer.</summary>
    private static readonly (byte Group, short Number)[] BlueBoxItems =
    [
        (0, 20), (0, 25), (0, 31), (2, 11), (2, 15), (4, 17), (4, 22), (5, 9), (5, 13),
        (10, 21), (11, 21), (10, 22), (11, 22),
        (12, 15), (14, 13), (14, 14), (14, 16), (14, 22), (14, 42),
    ];

    /// <summary>The armor sets among those normal items (helm, armor, pants, gloves and boots of each).</summary>
    private static readonly short[] BlueBoxArmorSets = [17, 24, 37, 38];

    /// <inheritdoc />
    public override string Name => HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Name;

    /// <inheritdoc />
    public override string Description => HNMO.Persistence.Initialization.Properties.PlugInResources.AuditDataFixesUpdatePlugIn_Description;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    protected override void ApplyVersionFixes(IContext context, GameConfiguration gameConfiguration)
    {
        AttachBlueBoxNormalItems(context, gameConfiguration);

        // A chance of 1 or more means every hit: such a value was written as a percent (10.0 for 10 %).
        foreach (var option in gameConfiguration.ItemSetGroups.Select(set => set.Options).OfType<ItemOptionDefinition>().SelectMany(o => o.PossibleOptions))
        {
            if (option.PowerUpDefinition is { TargetAttribute: { } target, Boost.ConstantValue: { } value }
                && target == Stats.ExcellentDamageChance
                && value.Value >= 1)
            {
                value.Value /= 100;
            }
        }

        foreach (var option in gameConfiguration.ItemOptions.SelectMany(o => o.PossibleOptions))
        {
            if (option.OptionType == ItemOptionTypes.GuardianOption
                && option.Number == (int)ItemGroups.Boots
                && option.PowerUpDefinition is { TargetAttribute: { } target, Boost.ConstantValue: { } value }
                && target == Stats.MaximumShield
                && value.Value < GuardianBootsShield)
            {
                value.Value = GuardianBootsShield;
            }
        }
    }

    // The normal-item groups of the Blue Chocolate Box (14:34 +0) and Deep Blue Candy Box (+1) were created but never
    // attached to the box (commit 1fc60d72 fixed the initializer only), so those boxes gave only Zen or excellent items.
    private static void AttachBlueBoxNormalItems(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Items.FirstOrDefault(i => i.Group == 14 && i.Number == 34) is not { } box)
        {
            return;
        }

        foreach (var (level, description) in new[]
                 {
                     (0, LocalizedString.FromResource(() => ItemDropDescriptions.BlueChocolateBoxNormalItems)),
                     (1, LocalizedString.FromResource(() => ItemDropDescriptions.DeepBlueCandyBoxNormalItems)),
                 })
        {
            if (box.DropItems.OfType<ItemDropItemGroup>().Any(g => g.ItemType == SpecialItemType.RandomItem && g.SourceItemLevel == level))
            {
                continue;
            }

            var group = context.CreateNew<ItemDropItemGroup>();
            group.ItemType = SpecialItemType.RandomItem;
            group.SourceItemLevel = (byte)level;
            group.Chance = 0.4;
            group.Description = description;
            group.MinimumLevel = 6;
            group.MaximumLevel = 9;
            group.DropEffect = ItemDropEffect.Fireworks;
            var items = BlueBoxItems
                .Concat(BlueBoxArmorSets.SelectMany(set => new[] { ItemGroups.Helm, ItemGroups.Armor, ItemGroups.Pants, ItemGroups.Gloves, ItemGroups.Boots }.Select(part => ((byte)part, set))));
            foreach (var (itemGroup, number) in items)
            {
                if (gameConfiguration.Items.FirstOrDefault(i => i.Group == itemGroup && i.Number == number) is { } definition
                    && !group.PossibleItems.Contains(definition))
                {
                    group.PossibleItems.Add(definition);
                }
            }

            box.DropItems.Add(group);
        }
    }
}
