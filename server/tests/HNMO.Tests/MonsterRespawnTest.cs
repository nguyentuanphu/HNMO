// <copyright file="MonsterRespawnTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.Pathfinding;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests for the respawn of a killed <see cref="Monster"/>.
/// </summary>
[TestFixture]
public class MonsterRespawnTest
{
    private static readonly TimeSpan RespawnDelay = TimeSpan.FromMilliseconds(100);

    private static readonly Point SpawnPoint = new(100, 100);

    /// <summary>
    /// Tests that a killed monster respawns on its map after the respawn delay.
    /// </summary>
    [Test]
    public async Task KilledMonsterRespawnsAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);

        await monster.ReflectDamageAsync(killer, 1_000_000).ConfigureAwait(false);
        Assert.That(monster.IsAlive, Is.False);

        // A respawning monster is alive a moment before it's back on the map: wait for both. Once back, it may walk
        // off its spawn point right away, so what counts is that it was seen there alive.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var seenBack = false;
        while (!seenBack && DateTime.UtcNow < deadline)
        {
            seenBack = monster.IsAlive && monster.CurrentMap.GetNpcsInRange(SpawnPoint, 1).Contains(monster);
            if (!seenBack)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        Assert.That(seenBack, Is.True, "the monster came back alive at its spawn point");
    }

    /// <summary>
    /// Tests that a killed monster which is disposed while it waits to respawn,
    /// e.g. because its spawn area got deleted, doesn't come back to the map.
    /// </summary>
    [Test]
    public async Task KilledMonsterDoesNotRespawnAfterDisposeAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var monster = await CreateMonsterAsync(gameContext).ConfigureAwait(false);
        var map = monster.CurrentMap;
        var monsterId = monster.Id;

        await monster.ReflectDamageAsync(killer, 1_000_000).ConfigureAwait(false);
        await monster.DisposeAsync().ConfigureAwait(false);

        await Task.Delay(RespawnDelay * 5).ConfigureAwait(false);

        Assert.That(monster.IsAlive, Is.False);
        Assert.That(map.GetNpcsInRange(SpawnPoint, 1), Does.Not.Contain(monster));
        Assert.That(map.GetAttackablesInRange(SpawnPoint, 1), Does.Not.Contain(monster));
        Assert.That(map.GetObject(monsterId), Is.Not.SameAs(monster));
    }

    /// <summary>
    /// Tests that hunting monsters in one part of a large multi-spawn field (such as Lorencia's 180..226 x 90..244
    /// area) does not drain that field by re-rolling each killed monster across the whole map quarter while the
    /// unobserved monsters at the far edge never move or die. Each monster respawns within its move range around
    /// its own initial spawn spot.
    /// </summary>
    [Test]
    public async Task KilledMonsterInLargeFieldRespawnsNearItsInitialSpotAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        var monsterDefinition = new MonsterDefinition
        {
            Id = Guid.NewGuid(),
            ObjectKind = NpcObjectKind.Monster,
            MoveRange = 3,
            RespawnDelay = TimeSpan.FromMilliseconds(20),
        };
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100 });

        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDefinition,
            GameMap = map.Definition,
            SpawnTrigger = SpawnTrigger.Automatic,
            X1 = 180,
            X2 = 226,
            Y1 = 90,
            Y2 = 244,
            Quantity = 1,
        };

        var monster = new Monster(
            spawnArea,
            monsterDefinition,
            map,
            NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object,
            gameContext.PlugInManager,
            gameContext.PathFinderPool);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        var initialSpot = monster.Position;

        for (var i = 0; i < 10; i++)
        {
            await monster.ReflectDamageAsync(killer, 1_000_000).ConfigureAwait(false);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!monster.IsAlive && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }

            Assert.That(monster.IsAlive, Is.True);
            var dx = Math.Abs(monster.Position.X - initialSpot.X);
            var dy = Math.Abs(monster.Position.Y - initialSpot.Y);
            Assert.That(
                Math.Max(dx, dy),
                Is.LessThanOrEqualTo(monsterDefinition.MoveRange),
                $"Respawn #{i + 1} moved the monster from its initial field spot {initialSpot} to {monster.Position}.");
        }
    }

    /// <summary>
    /// Tests that idle random wandering of a field monster stays anchored within the monster definition's move range
    /// of its initial <see cref="NonPlayerCharacter.SpawnPoint"/> instead of drifting away from the hunting ground.
    /// </summary>
    [Test]
    public async Task RandomMoveStaysAnchoredToInitialSpawnPointAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        var monsterDefinition = new MonsterDefinition
        {
            Id = Guid.NewGuid(),
            ObjectKind = NpcObjectKind.Monster,
            MoveRange = 3,
            MoveDelay = TimeSpan.FromMilliseconds(10),
        };
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100 });

        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDefinition,
            GameMap = map.Definition,
            SpawnTrigger = SpawnTrigger.Automatic,
            X1 = 180,
            X2 = 226,
            Y1 = 90,
            Y2 = 244,
            Quantity = 1,
        };

        var monster = new Monster(
            spawnArea,
            monsterDefinition,
            map,
            NullDropGenerator.Instance,
            new BasicMonsterIntelligence(),
            gameContext.PlugInManager,
            gameContext.PathFinderPool);
        monster.Initialize();
        monster.OnSpawn();
        await map.AddAsync(monster).ConfigureAwait(false);
        var homeSpot = monster.SpawnPoint;

        // Place the monster 20 tiles away from its home spot within the same large spawn area and trigger a random move.
        monster.Position = new Point((byte)Math.Min(225, homeSpot.X + 10), (byte)Math.Min(240, homeSpot.Y + 20));
        await monster.RandomMoveAsync().ConfigureAwait(false);
        if (monster.IsWalking)
        {
            var dx = Math.Abs(monster.WalkTarget.X - homeSpot.X);
            var dy = Math.Abs(monster.WalkTarget.Y - homeSpot.Y);
            Assert.That(Math.Max(dx, dy), Is.LessThanOrEqualTo(monsterDefinition.MoveRange));
        }
    }

    private static async ValueTask<Monster> CreateMonsterAsync(IGameContext gameContext)
    {
        // A unique id, because the attributes of monster definitions are cached by their id.
        var monsterDefinition = new MonsterDefinition
        {
            Id = Guid.NewGuid(),
            ObjectKind = NpcObjectKind.Monster,
            RespawnDelay = RespawnDelay,
        };
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100 });
        var map = await gameContext.GetMapAsync(0).ConfigureAwait(false);
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDefinition,
            GameMap = map!.Definition,
            SpawnTrigger = SpawnTrigger.Automatic,
            X1 = SpawnPoint.X,
            Y1 = SpawnPoint.Y,
            X2 = SpawnPoint.X,
            Y2 = SpawnPoint.Y,
            Quantity = 1,
        };

        var monster = new Monster(
            spawnArea,
            monsterDefinition,
            map,
            NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object,
            gameContext.PlugInManager,
            gameContext.PathFinderPool);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        return monster;
    }
}
