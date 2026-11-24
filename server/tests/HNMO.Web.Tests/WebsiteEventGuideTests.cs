// <copyright file="WebsiteEventGuideTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Configuration.Quests;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.InvasionEvents;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.API;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Verifies authoritative schedule, event reward and quest data.</summary>
[TestFixture]
public sealed class WebsiteEventGuideTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 1, 0, 0, DateTimeKind.Utc);

    /// <summary>Applies the runtime's allowed servers, time zone and featured-monster spawn map rule.</summary>
    [Test]
    public void CollectorUsesRuntimeServerAndSpawnRules()
    {
        using var persistence = new InMemoryPersistenceContextProvider().CreateNewContext();
        var world = persistence.CreateNew<GameConfiguration>();
        AddMap(persistence, world, 0, "First map");
        AddMap(persistence, world, 2, "Featured map");
        var featuredMonster = InvasionConfigurationDefaults.Golden.Mobs.Single(spawn => spawn.MapIds.Count > 1).MonsterId;
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        var plugin = manager.GetActivePlugInsOf<IPeriodicTaskPlugIn>().OfType<GoldenInvasionPlugIn>().Single();
        plugin.Configuration = new()
        {
            AllowedServerIds = [4], Timetable = [new TimeOnly(13, 30)], ForceSingleMap = true,
            Mobs = [new(20, 2, [0], SpawnMapStrategy.AllMaps), new(featuredMonster, 1, [2], SpawnMapStrategy.RandomMap)],
        };
        var game = new Mock<IGameContext>();
        game.SetupGet(context => context.Configuration).Returns(world);
        game.SetupGet(context => context.PlugInManager).Returns(manager);
        game.SetupGet(context => context.ServerTimeZone).Returns(TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney"));
        var timetable = new TimetableConfiguration { ShowMiniGames = false, ShowWorldBosses = false };
        var excluded = TimetableSourceCollector.CollectFromContext(game.Object, timetable, Now, 5);
        Assert.That(excluded.Where(row => row.Id == "invasion-golden").Select(row => row.InvasionConfiguration?.AllowedServerIds), Is.Empty);
        var included = TimetableSourceCollector.CollectFromContext(game.Object, timetable, Now, 4).Single(row => row.Id == "invasion-golden");
        Assert.That(included.ExplicitNextStartUtc, Is.EqualTo(Now.AddHours(1.5)));
        var spawns = included.ActiveInvasionSpawns!;
        Assert.That(spawns, Has.Count.EqualTo(2));
        Assert.That(spawns.SelectMany(spawn => spawn.MapIds).Distinct(), Is.EqualTo(new ushort[] { 2 }));
        Assert.That(spawns.All(spawn => !spawn.IsSpawnOnAllMaps), Is.True);
    }

    /// <summary>Publishes only configured rewards and objectives on active maps, preserving UTC instants.</summary>
    [Test]
    public void GuideUsesConfiguredRewardsAndQuests()
    {
        using var persistence = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = persistence.CreateNew<GameConfiguration>();
        var map = AddMap(persistence, config, 2, "Test field");
        var inactiveMap = AddMap(persistence, config, 17, "Hidden field"); inactiveMap.IsActive = false;
        var npc = persistence.CreateNew<MonsterDefinition>(); npc.Number = 22; npc.Designation = new("Test quest giver");
        var target = persistence.CreateNew<MonsterDefinition>(); target.Number = 29; target.Designation = new("Test monster"); config.Monsters.Add(target);
        var item = persistence.CreateNew<ItemDefinition>(); item.Name = new("Test token");
        var pool = persistence.CreateNew<DropItemGroup>(); pool.Chance = .15; pool.PossibleItems.Add(item); target.DropItemGroups.Add(pool);
        var spawn = persistence.CreateNew<MonsterSpawnArea>(); spawn.MonsterDefinition = npc; spawn.Quantity = 1;
        map.MonsterSpawns.Add(spawn); inactiveMap.MonsterSpawns.Add(spawn);
        var quest = persistence.CreateNew<QuestDefinition>(); quest.Name = new("Test task"); quest.MinimumCharacterLevel = 37; quest.RequiredStartMoney = 123;
        var kill = persistence.CreateNew<QuestMonsterKillRequirement>(); kill.Monster = target; kill.MinimumNumber = 9; quest.RequiredMonsterKills.Add(kill);
        var reward = persistence.CreateNew<QuestReward>(); reward.RewardType = QuestRewardType.Money; reward.Value = 1456; quest.Rewards.Add(reward);
        npc.Quests.Add(quest);
        var game = persistence.CreateNew<MiniGameDefinition>(); game.Type = MiniGameType.BloodCastle; game.MinimumCharacterLevel = 73;
        var gameReward = persistence.CreateNew<MiniGameReward>(); gameReward.RewardType = MiniGameRewardType.ExperiencePerRemainingSeconds;
        gameReward.RewardAmount = 37; gameReward.RequiredSuccess = MiniGameSuccessFlags.Winner; game.Rewards.Add(gameReward); config.MiniGameDefinitions.Add(game);
        var invasion = new PeriodicInvasionConfiguration { Mobs = [new(29, 5, [2], SpawnMapStrategy.AllMaps)] };
        var sources = new TimetableSourceItem[]
        {
            new() { Id = "event-bloodcastle", NameEn = "Test timed entry", ExplicitNextStartUtc = Now.AddHours(2), DropsEn = "Fallback reward text" },
            new() { Id = "invasion-test", Category = "invasion", NameEn = "Test invasion", ExplicitNextStartUtc = Now.AddHours(1), InvasionConfiguration = invasion, DropsEn = "Not an actual configured reward" },
        };
        var snapshot = TimetableEngine.BuildSnapshot(new(), true, Now, sources, []);
        var result = Project(config, [map, inactiveMap], snapshot, sources);
        var events = result["events"]!.AsArray();
        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[0]!["nextStartUtc"]!.GetValue<DateTime>(), Is.EqualTo(Now.AddHours(1)));
        Assert.That(events[0]!["invasion"]!["spawns"]![0]!["loot"]![0]!["en"]!.GetValue<string>(), Is.EqualTo("Test token"));
        Assert.That(events[1]!["tiers"]![0]!["minimumLevel"]!.GetValue<int>(), Is.EqualTo(73));
        Assert.That(events[1]!["tiers"]![0]!["rewards"]![0]!["kind"]!.GetValue<string>(), Is.EqualTo("ExperiencePerRemainingSeconds"));
        Assert.That(events[1]!["tiers"]![0]!["rewards"]![0]!["amount"]!.GetValue<int>(), Is.EqualTo(37));
        var quests = result["quests"]!.AsArray();
        Assert.That(quests, Has.Count.EqualTo(1));
        Assert.That(quests[0]!["maps"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That(quests[0]!["kills"]![0]!["count"]!.GetValue<int>(), Is.EqualTo(9));
        Assert.That(quests[0]!["rewards"]![0]!["value"]!.GetValue<int>(), Is.EqualTo(1456));
        Assert.That(result.ToJsonString(), Does.Not.Contain("Fallback reward text").And.Not.Contain("Not an actual configured reward").And.Not.Contain("Hidden field"));
    }

    /// <summary>Caps event output and leaves missing next-start data empty.</summary>
    [Test]
    public void ScheduleOutputIsPageBounded()
    {
        using var persistence = new InMemoryPersistenceContextProvider().CreateNewContext();
        var config = persistence.CreateNew<GameConfiguration>();
        var sources = Enumerable.Range(1, 40).Select(i => new TimetableSourceItem { Id = $"schedule-{i}" }).ToArray();
        var snapshot = TimetableEngine.BuildSnapshot(new(), true, Now, sources, []);
        var result = Project(config, [], snapshot, sources);
        Assert.That(result["eventsTruncated"]!.GetValue<bool>(), Is.True);
        Assert.That(result["events"]!.AsArray(), Has.Count.EqualTo(32));
        Assert.That(result["events"]![0]!["nextStartUtc"], Is.Null);
    }

    /// <summary>Uses localized admin labels in each supported administration locale.</summary>
    /// <param name="locale">The selected admin locale.</param>
    [TestCase("en")]
    [TestCase("vi")]
    [TestCase("de")]
    [TestCase("zh-CN")]
    public void SwitchMetadataIsLocalized(string locale)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(locale);
            Assert.That(WebsiteServerPageLabels.EventGuide, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.EventGuide)));
            Assert.That(WebsiteServerPageLabels.EventGuideDescription, Is.Not.EqualTo(nameof(WebsiteServerPageLabels.EventGuideDescription)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static GameMapDefinition AddMap(IContext persistence, GameConfiguration configuration, short number, string name)
    {
        var map = persistence.CreateNew<GameMapDefinition>(); map.Number = number; map.Name = new(name); configuration.Maps.Add(map);
        return map;
    }

    private static JsonObject Project(GameConfiguration configuration, GameMapDefinition[] maps, TimetableSnapshot snapshot, TimetableSourceItem[] sources)
        => JsonSerializer.SerializeToNode(WebsiteEventGuideProjection.Build(configuration, maps, snapshot, sources), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
}
