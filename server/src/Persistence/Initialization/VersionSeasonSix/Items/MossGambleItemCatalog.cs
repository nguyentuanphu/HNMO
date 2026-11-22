// <copyright file="MossGambleItemCatalog.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.VersionSeasonSix.Items;

using System.Linq.Expressions;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.Interfaces;
using HNMO.Persistence.Initialization.Items;
using HNMO.Persistence.Initialization.Properties;

/// <summary>
/// Creates the five Moss gamble voucher item definitions (13-71..75) and builds Moss the Merchant's (492) shop.
/// </summary>
internal static class MossGambleItemCatalog
{
    private const short MossNpcNumber = 492;
    private const byte VoucherGroup = 13;
    private const int PriceZen = 2_000_000;

    private static readonly (short Number, Expression<Func<string>> NameExpression, byte Slot)[] Vouchers =
    [
        (71, () => ItemNames.GambleSwordMaceSpear, 0),
        (72, () => ItemNames.GambleStaff, 2),
        (73, () => ItemNames.GambleBowCrossbow, 4),
        (74, () => ItemNames.GambleScepter, 6),
        (75, () => ItemNames.GambleStick, 24),
    ];

    /// <summary>
    /// Creates any missing Moss gamble voucher definitions (13-71..75).
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <param name="ensurePriceDefinition">True when running outside initial DB creation so items get a price definition immediately.</param>
    internal static void CreateItems(IContext context, GameConfiguration gameConfiguration, bool ensurePriceDefinition)
    {
        ItemPriceDefinition? priceDefinition = ensurePriceDefinition
            ? EnsureVoucherPriceDefinition(context, gameConfiguration)
            : null;

        foreach (var (number, nameExpression, _) in Vouchers)
        {
            var existing = gameConfiguration.Items.FirstOrDefault(i => i.Group == VoucherGroup && i.Number == number);
            if (existing is not null)
            {
                if (ensurePriceDefinition && existing.PriceDefinition is null)
                {
                    existing.PriceDefinition = priceDefinition;
                }

                continue;
            }

            var def = context.CreateNew<ItemDefinition>();
            def.Name = LocalizedString.FromResource(nameExpression);
            def.Group = VoucherGroup;
            def.Number = number;
            def.Width = 2;
            def.Height = 3;
            def.Durability = 255;
            def.DropsFromMonsters = false;
            def.MaximumItemLevel = 0;
            def.Value = 0;
            def.PriceDefinition = priceDefinition;
            foreach (var characterClass in gameConfiguration.CharacterClasses)
            {
                def.QualifiedCharacters.Add(characterClass);
            }

            def.SetGuid(def.Group, def.Number);
            gameConfiguration.Items.Add(def);
        }
    }

    /// <summary>
    /// Builds Moss's merchant store containing all five gamble vouchers.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="gameConfiguration">The game configuration.</param>
    /// <returns>The initialized merchant store.</returns>
    internal static ItemStorage CreateMerchantStore(IContext context, GameConfiguration gameConfiguration)
    {
        var storage = context.CreateNew<ItemStorage>();
        foreach (var (number, _, slot) in Vouchers)
        {
            var def = gameConfiguration.Items.FirstOrDefault(i => i.Group == VoucherGroup && i.Number == number);
            if (def is null)
            {
                continue;
            }

            var item = context.CreateNew<Item>();
            item.Definition = def;
            item.ItemSlot = slot;
            item.Durability = def.Durability;
            storage.Items.Add(item);
        }

        storage.SetGuid(MossNpcNumber);
        return storage;
    }

    private static ItemPriceDefinition EnsureVoucherPriceDefinition(IContext context, GameConfiguration gameConfiguration)
    {
        var name = ItemPriceDefinitionNames.ResourceManager.GetLocalizedString(nameof(ItemPriceDefinitionNames.MossGambleVoucher));
        if (gameConfiguration.ItemPriceDefinitions.FirstOrDefault(d =>
                d.Name.SourceKey == name.SourceKey || d.Name.ValueInNeutralLanguage == name.ValueInNeutralLanguage) is { } existing)
        {
            return existing;
        }

        // Avoid colliding with fixed/general GUID slots (1..35) on databases created before this special price existed.
        var price = context.CreateNew<ItemPriceDefinition>();
        short number = 200;
        do
        {
            price.SetGuid(number++);
        }
        while (gameConfiguration.ItemPriceDefinitions.Any(d => d.GetId() == price.GetId()));

        price.Name = name;
        price.BasePriceFormula = PriceZen.ToString(System.Globalization.CultureInfo.InvariantCulture);
        price.QuantityScaling = ItemPriceQuantityScaling.None;
        price.Modifiers = ItemPriceModifiers.None;
        gameConfiguration.ItemPriceDefinitions.Add(price);
        return price;
    }
}
