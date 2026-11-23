// <copyright file="WebsiteItemGuideTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.API;

/// <summary>Checks that the item guide reports authored values under the runtime's current rules.</summary>
[TestFixture]
public sealed class WebsiteItemGuideTests
{
    /// <summary>Uses live source restrictions and keeps configured box rewards and opening levels separate.</summary>
    [Test]
    public void DropAndBoxDataUseActualLevelRulesAndSettings()
    {
        using var persistence = new InMemoryPersistenceContextProvider().CreateNewContext();
        var world = persistence.CreateNew<GameConfiguration>();
        var item = AddItem(persistence, world, 3, 18, "Test item");
        item.DropLevel = 31;
        item.MaximumDropLevel = 62;
        var requirement = persistence.CreateNew<AttributeRequirement>();
        requirement.Attribute = Stats.TotalStrength;
        requirement.MinimumValue = 150;
        requirement.MinimumItemLevel = 4;
        item.Requirements.Add(requirement);
        var map = persistence.CreateNew<GameMapDefinition>(); map.Name = new("Test map");
        var eligible = AddMonster(persistence, map, 1, "Eligible monster", 45);
        var tooHigh = AddMonster(persistence, map, 2, "Out of range", 70);
        var direct = AddMonster(persistence, map, 3, "Old direct source", null);
        Assert.That(eligible.NumberOfMaximumItemDrops, Is.Positive);
        Assert.That(tooHigh.NumberOfMaximumItemDrops, Is.Positive);
        var mapGroup = persistence.CreateNew<DropItemGroup>(); mapGroup.Chance = 0.17; mapGroup.PossibleItems.Add(item); map.DropItemGroups.Add(mapGroup);
        var directGroup = persistence.CreateNew<DropItemGroup>(); directGroup.Chance = 0.33; directGroup.PossibleItems.Add(item); direct.DropItemGroups.Add(directGroup);
        var box = AddItem(persistence, world, 3, 19, "Test box");
        var boxPool = persistence.CreateNew<ItemDropItemGroup>();
        boxPool.SourceItemLevel = 5; boxPool.RequiredCharacterLevel = 61; boxPool.Chance = 0.5; boxPool.PossibleItems.Add(item);
        boxPool.MinimumLevel = 2; boxPool.MaximumLevel = 7;
        var money = persistence.CreateNew<ItemDropItemGroup>();
        money.SourceItemLevel = 5; money.RequiredCharacterLevel = 77; money.Chance = 0.5; money.ItemType = SpecialItemType.Money; money.MoneyAmount = 917;
        box.DropItems.Add(boxPool); box.DropItems.Add(money);

        var catalogue = new WebsiteItemGuideCatalogue(world, [map], true);
        var details = Read(catalogue, 3, 18)["item"]!;
        var sources = details["sources"]!.AsArray();
        Assert.That(sources, Has.Count.EqualTo(2));
        Assert.That(sources[0]!["mapPool"]!.GetValue<bool>(), Is.True);
        Assert.That(sources[1]!["monsterPool"]!.GetValue<bool>(), Is.True);
        Assert.That(sources[1]!["level"], Is.Null);
        Assert.That(details["requirements"]![0]!["value"]!.GetValue<int>(), Is.EqualTo(150));
        Assert.That(details["requirements"]![0]!["minimumItemLevel"]!.GetValue<byte>(), Is.EqualTo(4));
        Assert.That(details["foundInBoxes"]![0]!["level"]!.GetValue<byte>(), Is.EqualTo(5));
        var contents = Read(catalogue, 3, 19)["item"]!["contents"]![0]!;
        Assert.That(contents["requiredCharacterLevel"]!.GetValue<short>(), Is.EqualTo(77));
        Assert.That(contents["pools"]![0]!["maximumLevel"]!.GetValue<byte>(), Is.EqualTo(7));
        Assert.That(contents["pools"]![1]!["money"]!.GetValue<int>(), Is.EqualTo(917));
        Assert.That(Read(new(world, [map], false), 3, 19)["item"]!["contents"]!.AsArray(), Is.Empty);
        Assert.That(Read(new(world, [map], false), 3, 18)["item"]!["foundInBoxes"]!.AsArray(), Is.Empty);
    }

    /// <summary>Preserves settings-backed prices and constraints while leaving specialized quotes to the game.</summary>
    [Test]
    public void MixProjectionUsesActualIngredientAndResultSettings()
    {
        using var persistence = new InMemoryPersistenceContextProvider().CreateNewContext();
        var world = persistence.CreateNew<GameConfiguration>();
        var item = AddItem(persistence, world, 0, 1, "Test material");
        var map = persistence.CreateNew<GameMapDefinition>(); map.Name = new("Test workshop");
        var npc = AddMonster(persistence, map, 1, "Test crafter", null); npc.ObjectKind = NpcObjectKind.PassiveNpc;
        var mix = persistence.CreateNew<ItemCrafting>(); mix.Name = new("Test mix"); mix.Number = 1;
        var setup = persistence.CreateNew<SimpleCraftingSettings>(); setup.Money = 444; setup.SuccessPercent = 61; setup.MaximumSuccessPercent = 77;
        var material = persistence.CreateNew<ItemCraftingRequiredItem>(); material.PossibleItems.Add(item); material.MinimumAmount = 3;
        material.MaximumAmount = 4; material.MinimumItemLevel = 6; material.MaximumItemLevel = 9; material.Reference = 2;
        var option = persistence.CreateNew<ItemOptionType>(); option.Name = new("Test option"); material.RequiredItemOptions.Add(option); setup.RequiredItems.Add(material);
        var result = persistence.CreateNew<ItemCraftingResultItem>(); result.Reference = 2; result.AddLevel = 1; setup.ResultItems.Add(result);
        mix.SimpleCraftingSettings = setup; npc.ItemCraftings.Add(mix);
        var specialized = persistence.CreateNew<ItemCrafting>(); specialized.Name = new("Test special mix"); specialized.Number = 2;
        specialized.ItemCraftingHandlerClassName = "Specialized.TestHandler"; specialized.SimpleCraftingSettings = setup; npc.ItemCraftings.Add(specialized);
        var catalogue = new WebsiteItemGuideCatalogue(world, [map], true);

        var settings = Read(catalogue, 0, 1, 0)["mix"]!["settings"]!;
        Assert.That(settings["basePercent"]!.GetValue<int>(), Is.EqualTo(61));
        Assert.That(settings["maximumPercent"]!.GetValue<int>(), Is.EqualTo(77));
        Assert.That(settings["fixedZen"]!.GetValue<int>(), Is.EqualTo(444));
        Assert.That(settings["ingredients"]![0]!["minimumAmount"]!.GetValue<byte>(), Is.EqualTo(3));
        Assert.That(settings["ingredients"]![0]!["options"]![0]!["en"]!.GetValue<string>(), Is.EqualTo("Test option"));
        Assert.That(settings["results"]![0]!["reference"]!.GetValue<byte>(), Is.EqualTo(2));
        Assert.That(Read(catalogue, 0, 1, 1)["mix"]!["settings"], Is.Null);
    }

    /// <summary>Paginates large item groups and rejects missing exact choices.</summary>
    [Test]
    public void ActiveCataloguesArePagedAndUnknownChoicesReturnNoData()
    {
        using var persistence = new InMemoryPersistenceContextProvider().CreateNewContext();
        var world = persistence.CreateNew<GameConfiguration>();
        for (short id = 1; id <= 30; id++)
        {
            AddItem(persistence, world, 2, id, $"Item {id}");
        }

        AddItem(persistence, world, 2, 31, "Disabled item").IsActive = false;
        var catalogue = new WebsiteItemGuideCatalogue(world, [], false);
        var first = Read(catalogue, 2, null);
        Assert.That(first["items"]!.AsArray(), Has.Count.EqualTo(24));
        Assert.That(first["itemPages"]!.GetValue<int>(), Is.EqualTo(2));
        Assert.That(Read(catalogue, 2, 30)["itemPage"]!.GetValue<int>(), Is.EqualTo(2));
        Assert.That(catalogue.Read(2, 1, 31, 1, null), Is.Null);
    }

    /// <summary>Uses the chosen administrative locale for the new setting.</summary>
    /// <param name="locale">The requested admin locale.</param>
    [TestCase("en")]
    [TestCase("vi")]
    [TestCase("de")]
    [TestCase("zh-CN")]
    public void GuideSwitchUsesNativeLabels(string locale)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
            Assert.That(WebsiteServerPageLabels.ItemGuide, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.ItemGuide)));
            Assert.That(WebsiteServerPageLabels.ItemGuideDescription, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.ItemGuideDescription)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static ItemDefinition AddItem(IContext context, GameConfiguration configuration, byte group, short number, string name)
    {
        var item = context.CreateNew<ItemDefinition>(); item.Group = group; item.Number = number; item.Name = new(name);
        configuration.Items.Add(item);
        return item;
    }

    private static MonsterDefinition AddMonster(IContext context, GameMapDefinition map, short number, string name, int? level)
    {
        var monster = context.CreateNew<MonsterDefinition>(); monster.Number = number; monster.Designation = new(name); monster.NumberOfMaximumItemDrops = 1;
        if (level is { } value)
        {
            var attribute = context.CreateNew<MonsterAttribute>(); attribute.AttributeDefinition = Stats.Level; attribute.Value = value; monster.Attributes.Add(attribute);
        }

        var spawn = context.CreateNew<MonsterSpawnArea>(); spawn.MonsterDefinition = monster; spawn.Quantity = 1; map.MonsterSpawns.Add(spawn);
        return monster;
    }

    private static JsonObject Read(WebsiteItemGuideCatalogue source, int group, int? number, int? recipe = null)
        => JsonSerializer.SerializeToNode(source.Read(group, 1, number, 1, recipe), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
}
