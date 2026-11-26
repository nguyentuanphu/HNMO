// <copyright file="AuditDataFixesUpdateTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// <see cref="AuditDataFixesUpdatePlugInSeason6"/>: the data fixes of the 25 Nov 2026 audit (Kantata's excellent damage
/// chance, the SD of the Guardian option on boots, Hanzo's store, the normal items of the Blue Chocolate / Deep Blue
/// Candy Box) reach databases created before them.
/// </summary>
[TestFixture]
internal class AuditDataFixesUpdateTest
{
    /// <summary>A new database already has the fixed data.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheFixedDataAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        AssertFixed(gameConfiguration);
    }

    /// <summary>A database with the old data is repaired; a second run changes nothing more.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateRepairsAnOldDatabaseOnceAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);

        // The data as it was before the audit fix.
        ExcellentChances(gameConfiguration).Single(v => Math.Abs(v.Value - 0.10f) < 0.0001 && IsKantata(gameConfiguration, v)).Value = 10.0f;
        GuardianBootsShield(gameConfiguration).Value = 200;
        var store = Hanzo(gameConfiguration);
        Sword(store, 7).ItemSlot = 73;
        Sword(store, 8).ItemSlot = 74;
        Sword(store, 5).ItemSlot = 75;
        store.Items.Single(i => i.ItemSlot == 50).Definition = gameConfiguration.Items.Single(d => d.Group == 2 && d.Number == 1);
        var blueBox = BlueBox(gameConfiguration);
        foreach (var normal in NormalGroups(blueBox).ToList())
        {
            blueBox.DropItems.Remove(normal); // as before the fix: the normal-item groups were never attached
        }

        var update = new AuditDataFixesUpdatePlugInSeason6();
        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        AssertFixed(gameConfiguration);

        await update.ApplyUpdateAsync(contextProvider.CreateNewContext(), gameConfiguration).ConfigureAwait(false);
        AssertFixed(gameConfiguration);
        Assert.That(update.IsMandatory, Is.True);
    }

    private static void AssertFixed(GameConfiguration gameConfiguration)
    {
        var store = Hanzo(gameConfiguration);
        Assert.Multiple(() =>
        {
            Assert.That(ExcellentChances(gameConfiguration).Select(v => v.Value), Is.All.LessThan(1), "a chance of 1 or more means every hit");
            Assert.That(GuardianBootsShield(gameConfiguration).Value, Is.EqualTo(700));
            Assert.That(store.Items.GroupBy(i => i.ItemSlot).Where(g => g.Count() > 1), Is.Empty, "one item per store slot");
            Assert.That(new[] { Sword(store, 7).ItemSlot, Sword(store, 8).ItemSlot, Sword(store, 5).ItemSlot }, Is.EqualTo(new byte[] { 74, 75, 76 }));
            Assert.That(store.Items.Single(i => i.ItemSlot == 50).Definition!.Number, Is.EqualTo(0), "slot 50 holds the Mace");
            var normals = NormalGroups(BlueBox(gameConfiguration)).ToList();
            Assert.That(normals.Select(g => (int)g.SourceItemLevel).OrderBy(l => l), Is.EqualTo(new[] { 0, 1 }), "one normal-item group per box level, once");
            Assert.That(normals.Select(g => g.Chance), Is.All.EqualTo(0.4));
            Assert.That(normals.Select(g => g.PossibleItems.Count), Is.All.EqualTo(38), "19 items and the parts of 4 armor sets, as a new database has them (one set has no helm)");
            Assert.That(normals.All(g => g.PossibleItems.Any(i => i.Group == 14 && i.Number == 13)), Is.True, "the jewels are in it");
        });
    }

    private static IEnumerable<HNMO.AttributeSystem.SimpleElement> ExcellentChances(GameConfiguration gameConfiguration)
        => gameConfiguration.ItemSetGroups.Select(s => s.Options).OfType<ItemOptionDefinition>().SelectMany(o => o.PossibleOptions)
            .Where(o => o.PowerUpDefinition?.TargetAttribute == Stats.ExcellentDamageChance)
            .Select(o => o.PowerUpDefinition!.Boost!.ConstantValue);

    private static bool IsKantata(GameConfiguration gameConfiguration, HNMO.AttributeSystem.SimpleElement value)
        => gameConfiguration.ItemSetGroups.Any(s => s.Options is { } options
            && options.PossibleOptions.Any(o => ReferenceEquals(o.PowerUpDefinition?.Boost?.ConstantValue, value))
            && options.PossibleOptions.Any(o => o.PowerUpDefinition?.TargetAttribute == Stats.TotalVitality && o.PowerUpDefinition.Boost?.ConstantValue.Value == 30.0f));

    private static HNMO.AttributeSystem.SimpleElement GuardianBootsShield(GameConfiguration gameConfiguration)
        => gameConfiguration.ItemOptions.SelectMany(o => o.PossibleOptions)
            .Single(o => o.OptionType == ItemOptionTypes.GuardianOption && o.Number == 11 && o.PowerUpDefinition?.TargetAttribute == Stats.MaximumShield)
            .PowerUpDefinition!.Boost!.ConstantValue;

    private static ItemDefinition BlueBox(GameConfiguration gameConfiguration)
        => gameConfiguration.Items.Single(i => i.Group == 14 && i.Number == 34);

    private static IEnumerable<ItemDropItemGroup> NormalGroups(ItemDefinition box)
        => box.DropItems.OfType<ItemDropItemGroup>().Where(g => g.ItemType == SpecialItemType.RandomItem);

    private static ItemStorage Hanzo(GameConfiguration gameConfiguration)
        => gameConfiguration.Monsters.Single(m => m.Number == AuditDataFixesUpdatePlugInBase.HanzoNumber).MerchantStore!;

    private static Item Sword(ItemStorage store, short number)
        => store.Items.Single(i => i.Definition is { Group: 0 } d && d.Number == number);

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
