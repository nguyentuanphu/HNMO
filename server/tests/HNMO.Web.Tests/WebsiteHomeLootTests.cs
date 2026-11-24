// <copyright file="WebsiteHomeLootTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.GameLogic.Attributes;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.API;

/// <summary>Home projection tests for reachable drop groups and configured equipment sources.</summary>
[TestFixture]
public sealed class WebsiteHomeLootTests
{
    /// <summary>Only eligible active field sources count, and competing base rolls are scaled as in the game.</summary>
    [Test]
    public void UsesActiveSpawnsAndRealDropCompetition()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = context.CreateNew<GameConfiguration>();
        var map = context.CreateNew<GameMapDefinition>();
        map.Name = new("Fixture forest");
        var monster = context.CreateNew<MonsterDefinition>();
        monster.Designation = new("Fixture monster");
        monster.NumberOfMaximumItemDrops = 2;
        var level = context.CreateNew<MonsterAttribute>();
        level.AttributeDefinition = Stats.Level;
        level.Value = 45;
        monster.Attributes.Add(level);
        var spawn = context.CreateNew<MonsterSpawnArea>();
        spawn.MonsterDefinition = monster;
        spawn.Quantity = 1;
        map.MonsterSpawns.Add(spawn);

        var slot = context.CreateNew<ItemSlotType>();
        slot.Description = new("Main hand");
        var sword = context.CreateNew<ItemDefinition>();
        sword.Name = new("Fixture sword");
        sword.ItemSlot = slot;
        sword.MaximumItemLevel = 11;
        var inactive = context.CreateNew<ItemDefinition>();
        inactive.Name = new("Inactive exemplar");
        inactive.ItemSlot = slot;
        inactive.MaximumItemLevel = 15;
        inactive.IsActive = false;

        var guaranteed = CreateGroup(context, "Guaranteed group", 1, 4, sword);
        var competitive = CreateGroup(context, "Competitive group", 0.8, 15, sword, inactive);
        var money = CreateGroup(context, "Money", 0.8, 0);
        money.ItemType = SpecialItemType.Money;
        map.DropItemGroups.Add(guaranteed);
        map.DropItemGroups.Add(competitive);
        map.DropItemGroups.Add(money);
        var aboveSpawn = CreateGroup(context, "Outside level band", 1, 15, sword);
        aboveSpawn.MinimumMonsterLevel = 46;
        map.DropItemGroups.Add(aboveSpawn);

        var projection = Project(config, map);
        var gear = projection["gear"]!.AsArray();
        Assert.That(gear, Has.Count.EqualTo(1));
        Assert.That(gear[0]!["nameEn"]!.GetValue<string>(), Is.EqualTo("Fixture sword"));
        Assert.That(gear[0]!["maximumLevel"]!.GetValue<int>(), Is.EqualTo(11));
        Assert.That(gear[0]!["sourceEn"]!.GetValue<string>(), Is.EqualTo("Fixture monster"));
        var groups = projection["groups"]!.AsArray();
        Assert.That(groups, Has.Count.EqualTo(2));
        Assert.That(groups[0]!["maximumChance"]!.GetValue<double>(), Is.EqualTo(1));
        Assert.That(groups[1]!["maximumChance"]!.GetValue<double>(), Is.EqualTo(0.5));

        monster.NumberOfMaximumItemDrops = 1;
        projection = Project(config, map);
        Assert.That(projection["groups"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That(projection["gear"]![0]!["maximumLevel"]!.GetValue<int>(), Is.EqualTo(4));

        spawn.SpawnTrigger = SpawnTrigger.OnceAtEventStart;
        Assert.That(Project(config, map)["gear"]!.AsArray(), Is.Empty);
    }

    /// <summary>Bundles require every product to be deliverable and keep the package price.</summary>
    [Test]
    public void UsesTheShopDeliveryGateForWholeBundles()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = context.CreateNew<GameConfiguration>();
        var shop = context.CreateNew<CashShopConfiguration>();
        config.CashShopConfiguration = shop;
        var package = context.CreateNew<CashShopPackage>();
        package.Price = 70;
        package.IsForSale = true;
        package.IsBundle = true;
        shop.Packages.Add(package);
        var first = context.CreateNew<CashShopProduct>();
        first.ItemDefinition = context.CreateNew<ItemDefinition>();
        first.ItemDefinition.Name = new("Visible first");
        package.Products.Add(first);
        var second = context.CreateNew<CashShopProduct>();
        second.ItemDefinition = context.CreateNew<ItemDefinition>();
        second.ItemDefinition.Name = new("Visible second");
        package.Products.Add(second);

        var unavailable = CatalogProducts(config, product => product != second);
        Assert.That(unavailable, Is.Empty);
        var available = CatalogProducts(config, _ => true);
        Assert.That(available, Has.Count.EqualTo(2));
        Assert.That(available.Select(item => item!["price"]!.GetValue<int>()), Is.All.EqualTo(70));
    }

    /// <summary>Ingredient lists use the recipe's ranges and filter inactive options without returning fabricated alternatives.</summary>
    [Test]
    public void MixMaterialsComeFromTheSettings()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var npc = context.CreateNew<MonsterDefinition>();
        npc.Designation = new("Fixture crafter");
        var recipe = context.CreateNew<ItemCrafting>();
        recipe.Name = new("Fixture mix");
        var settings = context.CreateNew<SimpleCraftingSettings>();
        recipe.SimpleCraftingSettings = settings;
        npc.ItemCraftings.Add(recipe);
        var material = context.CreateNew<ItemCraftingRequiredItem>();
        material.MinimumAmount = 2;
        material.MaximumAmount = 4;
        material.MinimumItemLevel = 3;
        material.MaximumItemLevel = 5;
        settings.RequiredItems.Add(material);
        var item = context.CreateNew<ItemDefinition>();
        item.Name = new("Fixture material");
        material.PossibleItems.Add(item);
        var inactive = context.CreateNew<ItemDefinition>();
        inactive.IsActive = false;
        material.PossibleItems.Add(inactive);
        var output = context.CreateNew<ItemCraftingResultItem>();
        output.ItemDefinition = item;
        settings.ResultItems.Add(output);

        var projection = JsonSerializer.SerializeToNode(WebsiteServerGuideExtrasProjection.Build([npc], false, false, false), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var ingredient = projection["crafts"]![0]!["ingredients"]![0]!;
        Assert.That(ingredient["minimumAmount"]!.GetValue<int>(), Is.EqualTo(2));
        Assert.That(ingredient["maximumItemLevel"]!.GetValue<int>(), Is.EqualTo(5));
        Assert.That(ingredient["anyItem"]!.GetValue<bool>(), Is.False);
        Assert.That(ingredient["items"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That(ingredient["items"]![0]!["nameEn"]!.GetValue<string>(), Is.EqualTo("Fixture material"));
    }

    private static DropItemGroup CreateGroup(IContext context, string name, double chance, byte level, params ItemDefinition[] items)
    {
        var group = context.CreateNew<DropItemGroup>();
        group.Description = new(name);
        group.Chance = chance;
        group.ItemLevel = level;
        foreach (var item in items)
        {
            group.PossibleItems.Add(item);
        }

        return group;
    }

    private static JsonNode Project(GameConfiguration config, GameMapDefinition map)
        => JsonSerializer.SerializeToNode(WebsiteHomeLootProjection.Build(config, [map], []), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static JsonArray CatalogProducts(GameConfiguration config, Func<CashShopProduct, bool> canDeliver)
        => JsonSerializer.SerializeToNode(WebsiteServerGuideProjection.Build(config, null, null, null, null, canDeliver, null), new JsonSerializerOptions(JsonSerializerDefaults.Web))!["catalog"]!["products"]!.AsArray();
}
