// <copyright file="AddChaosCardMixesUpdatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using HNMO.GameLogic.CashShop;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.Persistence.Initialization.VersionSeasonSix.Items;
using HNMO.PlugIns;

/// <summary>
/// Adds the four Chaos Card items (<c>14:54, 14:92, 14:93, 14:95</c>), wires the
/// Chaos Card Master combination (<c>#375</c>, <c>MixType = 40</c>), and enables
/// the matching in-game shop packages.
/// </summary>
[PlugIn]
[Display(
    Name = nameof(Properties.PlugInResources.AddChaosCardMixesUpdatePlugIn_Name),
    Description = nameof(Properties.PlugInResources.AddChaosCardMixesUpdatePlugIn_Description),
    ResourceType = typeof(Properties.PlugInResources))]
[Guid("B62E6D7B-54F9-4F0F-965E-F99395C00040")]
public class AddChaosCardMixesUpdatePlugIn : UpdatePlugInBase
{
    private static readonly IReadOnlyDictionary<int, short> ProductItemNumbers = new Dictionary<int, short>
    {
        [32] = ChaosCardItems.ChaosCardGoldNumber,
        [34] = ChaosCardItems.ChaosCardMiniNumber,
        [530] = ChaosCardItems.ChaosCardGoldNumber,
        [534] = ChaosCardItems.ChaosCardGoldNumber,
        [535] = ChaosCardItems.ChaosCardGoldNumber,
        [536] = ChaosCardItems.ChaosCardGoldNumber,
        [537] = ChaosCardItems.ChaosCardGoldNumber,
        [538] = ChaosCardItems.ChaosCardGoldNumber,
        [539] = ChaosCardItems.ChaosCardGoldNumber,
        [540] = ChaosCardItems.ChaosCardGoldNumber,
        [541] = ChaosCardItems.ChaosCardGoldNumber,
        [542] = ChaosCardItems.ChaosCardGoldNumber,
        [543] = ChaosCardItems.ChaosCardGoldNumber,
        [544] = ChaosCardItems.ChaosCardGoldNumber,
        [545] = ChaosCardItems.ChaosCardGoldNumber,
        [546] = ChaosCardItems.ChaosCardMiniNumber,
        [547] = ChaosCardItems.ChaosCardMiniNumber,
        [548] = ChaosCardItems.ChaosCardMiniNumber,
        [549] = ChaosCardItems.ChaosCardMiniNumber,
        [550] = ChaosCardItems.ChaosCardMiniNumber,
        [551] = ChaosCardItems.ChaosCardMiniNumber,
        [552] = ChaosCardItems.ChaosCardMiniNumber,
        [553] = ChaosCardItems.ChaosCardMiniNumber,
        [554] = ChaosCardItems.ChaosCardMiniNumber,
        [555] = ChaosCardItems.ChaosCardMiniNumber,
        [556] = ChaosCardItems.ChaosCardMiniNumber,
        [557] = ChaosCardItems.ChaosCardMiniNumber,
        [558] = ChaosCardItems.ChaosCardMiniNumber,
    };

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => "Add Chaos Card items and mixes";

    /// <inheritdoc />
    public override string Description =>
        "Adds the four Chaos Cards (14-54, 14-92, 14-93, 14-95), wires the Chaos Card Master combination (MixType 40), and activates their in-game shop packages.";

    /// <inheritdoc />
    public override bool IsMandatory => false;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 26, 9, 40, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IEnumerable<UpdateDependency> DependsOn => [typeof(AddItemRuleFlagsPlugIn).GUID];

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var initializer = new ChaosCardItems(context, gameConfiguration);
        var addedItems = initializer.CreateItems();
        ItemRules.Apply(addedItems);
        initializer.AddCraftings();
        AssignCashShopProducts(gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private static void AssignCashShopProducts(GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.CashShopConfiguration is not { } configuration
            || !CashShopInitializer.UsesClientScript(configuration))
        {
            return;
        }

        foreach (var package in configuration.Packages)
        {
            var assigned = false;
            foreach (var product in package.Products.Where(p => p.ItemDefinition is null))
            {
                if (ProductItemNumbers.TryGetValue(product.ProductSequence, out var number)
                    && gameConfiguration.Items.FirstOrDefault(item => item.Group == ChaosCardItems.ItemGroup && item.Number == number) is { } item)
                {
                    product.ItemDefinition = item;
                    assigned = true;
                }
            }

            if (assigned && package.Products.All(product => product.CanBeDelivered()))
            {
                package.IsForSale = true;
            }
        }
    }
}
