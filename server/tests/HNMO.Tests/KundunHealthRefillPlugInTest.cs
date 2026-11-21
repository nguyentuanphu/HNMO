// <copyright file="KundunHealthRefillPlugInTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using HNMO.Pathfinding;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;
using MonsterSpawnArea = HNMO.Persistence.BasicModel.MonsterSpawnArea;

/// <summary>
/// Kundun gets health back over time, as in the Season 6/9 server suites.
/// </summary>
[TestFixture]
public class KundunHealthRefillPlugInTest
{
    private const short KundunNumber = 275;

    /// <summary>
    /// The default settings are the suites' KundunRefillHPSec, KundunRefillHP and KundunRefillHPTime.
    /// </summary>
    [Test]
    public void DefaultsAreTheSuiteValues()
    {
        var configuration = (KundunHealthRefillConfiguration)new KundunHealthRefillPlugIn().CreateDefaultConfig();

        Assert.Multiple(() =>
        {
            Assert.That(configuration.MonsterNumber, Is.EqualTo(KundunNumber));
            Assert.That(configuration.HealthPerSecond, Is.EqualTo(400));
            Assert.That(configuration.HealthPerInterval, Is.EqualTo(15_000));
            Assert.That(configuration.Interval, Is.EqualTo(TimeSpan.FromSeconds(600)));
        });
    }

    /// <summary>
    /// A hurt Kundun gets the health per second back on each tick, but not more than its maximum health.
    /// </summary>
    [Test]
    public async Task HurtKundunGetsHealthBackAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var (map, kundun) = await CreateMonsterAsync(gameContext, KundunNumber).ConfigureAwait(false);
        var (_, other) = await CreateMonsterAsync(gameContext, 274, new Point(160, 160)).ConfigureAwait(false);
        kundun.Health = 1000;
        other.Health = 1000;

        var restored = KundunHealthRefillPlugIn.Refill(map, KundunNumber, 400);

        Assert.That(restored, Is.EqualTo(400));
        Assert.That(kundun.Health, Is.EqualTo(1400));
        Assert.That(other.Health, Is.EqualTo(1000), "another monster");

        KundunHealthRefillPlugIn.Refill(map, KundunNumber, 10_000_000);
        Assert.That(kundun.Health, Is.EqualTo(5_000_000));
    }

    /// <summary>
    /// The first run of the plug-in gives Kundun the health per second.
    /// </summary>
    [Test]
    public async Task PeriodicTaskRefillsKundunAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var (_, kundun) = await CreateMonsterAsync(gameContext, KundunNumber).ConfigureAwait(false);
        kundun.Health = 1000;

        await new KundunHealthRefillPlugIn().ExecuteTaskAsync((GameContext)gameContext).ConfigureAwait(false);

        Assert.That(kundun.Health, Is.EqualTo(1400));
    }

    private static async ValueTask<(GameMap Map, Monster Monster)> CreateMonsterAsync(IGameContext gameContext, short number, Point? position = null)
    {
        var map = await gameContext.GetMapAsync(0).ConfigureAwait(false);
        var point = position ?? new Point(150, 150);
        var monsterDefinition = new MonsterDefinition
        {
            Id = Guid.NewGuid(),
            Number = number,
            ObjectKind = NpcObjectKind.Monster,
            AttackDelay = TimeSpan.FromSeconds(10),
        };
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 5_000_000 });
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDefinition,
            GameMap = map!.Definition,
            X1 = point.X,
            Y1 = point.Y,
            X2 = point.X,
            Y2 = point.Y,
            Quantity = 1,
        };
        map.Definition.MonsterSpawns.Add(spawnArea);

        var monster = new Monster(spawnArea, monsterDefinition, map, NullDropGenerator.Instance, new NullMonsterIntelligence(), gameContext.PlugInManager, gameContext.PathFinderPool);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        return (map, monster);
    }
}
