// <copyright file="MonsterAmountTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using HNMO.Pathfinding;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;

/// <summary>
/// Tests for the monster amount multiplier per map (<see cref="MonsterAmountPlugIn"/>).
/// </summary>
[TestFixture]
public class MonsterAmountTest
{
    private static readonly Point Center = new(100, 100);

    /// <summary>
    /// Tests the multiplication and rounding of the spawn quantities.
    /// </summary>
    /// <param name="quantity">The stored quantity.</param>
    /// <param name="multiplier">The multiplier.</param>
    /// <param name="expected">The expected quantity.</param>
    [TestCase(10, 1.0, 10)]
    [TestCase(10, 2.0, 20)]
    [TestCase(7, 3.0, 21)]
    [TestCase(4, 5.0, 20)]
    [TestCase(10, 0.5, 5)]
    [TestCase(3, 0.5, 2)]
    [TestCase(5, 0.5, 3)]
    [TestCase(1, 0.5, 1)]
    [TestCase(0, 3.0, 0)]
    [TestCase(4, 10.0, 20)]
    [TestCase(4, 0.1, 2)]
    [TestCase(4, -2.0, 4)]
    [TestCase(4, double.NaN, 4)]
    public void ApplyMultipliesAndRounds(int quantity, double multiplier, int expected)
    {
        Assert.That(MonsterAmountConfiguration.Apply(quantity, multiplier), Is.EqualTo(expected));
    }

    /// <summary>
    /// Tests that setting the multiplier 1 removes a map's entry, and that unknown maps have the multiplier 1.
    /// </summary>
    [Test]
    public void SetMultiplierStoresOneEntryPerMap()
    {
        var configuration = new MonsterAmountConfiguration();
        Assert.That(configuration.GetMultiplier(3), Is.EqualTo(1));

        configuration.SetMultiplier(3, 2);
        configuration.SetMultiplier(3, 4);
        configuration.SetMultiplier(7, 0.5);
        Assert.That(configuration.Maps, Has.Count.EqualTo(2));
        Assert.That(configuration.GetMultiplier(3), Is.EqualTo(4));
        Assert.That(configuration.GetMultiplier(7), Is.EqualTo(0.5));

        configuration.SetMultiplier(3, 1);
        Assert.That(configuration.Maps, Has.Count.EqualTo(1));
        Assert.That(configuration.GetMultiplier(3), Is.EqualTo(1));
    }

    /// <summary>
    /// Tests that a map spawns the multiplied amount of monsters, that a changed multiplier applies at once
    /// to the running map, and that ×1 brings back the original amount. NPCs aren't multiplied.
    /// </summary>
    [Test]
    public async Task MapSpawnsMultipliedAmountAndFollowsChangesAsync()
    {
        var amounts = new MonsterAmountConfiguration();
        amounts.SetMultiplier(0, 2);
        var plugInConfiguration = new HNMO.Persistence.BasicModel.PlugInConfiguration { TypeId = typeof(MonsterAmountPlugIn).GUID, IsActive = true };
        plugInConfiguration.SetConfiguration(amounts, null);

        var (gameContext, initializer) = CreateGameContext(plugInConfiguration, monsterQuantity: 4, npcQuantity: 1);
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        Assert.That(CountMonsters(map), Is.EqualTo(8));
        Assert.That(CountOtherNpcs(map), Is.EqualTo(1));

        amounts.SetMultiplier(0, 0.5);
        plugInConfiguration.SetConfiguration(amounts, null);
        gameContext.PlugInManager.ConfigurePlugIn(typeof(MonsterAmountPlugIn).GUID, plugInConfiguration);
        await initializer.ApplyMonsterAmountsAsync().ConfigureAwait(false);
        Assert.That(CountMonsters(map), Is.EqualTo(2));

        amounts.SetMultiplier(0, 1);
        plugInConfiguration.SetConfiguration(amounts, null);
        gameContext.PlugInManager.ConfigurePlugIn(typeof(MonsterAmountPlugIn).GUID, plugInConfiguration);
        await initializer.ApplyMonsterAmountsAsync().ConfigureAwait(false);
        Assert.That(CountMonsters(map), Is.EqualTo(4));
        Assert.That(CountOtherNpcs(map), Is.EqualTo(1));
        Assert.That(map.Definition.MonsterSpawns.Sum(s => s.Quantity), Is.EqualTo(5), "The stored quantities stay as they are.");

        amounts.SetMultiplier(0, 3);
        plugInConfiguration.SetConfiguration(amounts, null);
        gameContext.PlugInManager.ConfigurePlugIn(typeof(MonsterAmountPlugIn).GUID, plugInConfiguration);
        await initializer.ApplyMonsterAmountsAsync().ConfigureAwait(false);
        Assert.That(CountMonsters(map), Is.EqualTo(12));

        gameContext.PlugInManager.DeactivatePlugIn<MonsterAmountPlugIn>();
        await initializer.ApplyMonsterAmountsAsync().ConfigureAwait(false);
        Assert.That(CountMonsters(map), Is.EqualTo(4));
    }

    /// <summary>
    /// Tests that without a configuration the stored quantities spawn.
    /// </summary>
    [Test]
    public async Task MapWithoutConfigurationSpawnsStoredAmountAsync()
    {
        var (gameContext, _) = CreateGameContext(null, monsterQuantity: 3, npcQuantity: 0);
        var map = (await gameContext.GetMapAsync(0).ConfigureAwait(false))!;
        Assert.That(CountMonsters(map), Is.EqualTo(3));
    }

    private static int CountMonsters(GameMap map) => map.GetNpcsInRange(Center, 50).Count(n => n is Monster && n.Definition.ObjectKind == NpcObjectKind.Monster);

    private static int CountOtherNpcs(GameMap map) => map.GetNpcsInRange(Center, 50).Count(n => n.Definition.ObjectKind != NpcObjectKind.Monster);

    private static (IGameContext GameContext, MapInitializer Initializer) CreateGameContext(PlugInConfiguration? amountConfiguration, int monsterQuantity, int npcQuantity)
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var context = contextProvider.CreateNewContext();
        var gameConfig = context.CreateNew<HNMO.Persistence.BasicModel.GameConfiguration>();
        var mapDef = context.CreateNew<HNMO.Persistence.BasicModel.GameMapDefinition>();
        mapDef.Number = 0;
        mapDef.TerrainData = new byte[ushort.MaxValue + 3];
        gameConfig.Maps.Add(mapDef);
        gameConfig.MaximumPartySize = 5;
        gameConfig.RecoveryInterval = int.MaxValue;
        gameConfig.ItemDropDuration = TimeSpan.FromMinutes(1);

        var monster = new HNMO.Persistence.BasicModel.MonsterDefinition { Id = Guid.NewGuid(), Number = 1, ObjectKind = NpcObjectKind.Monster, RespawnDelay = TimeSpan.FromSeconds(10) };
        monster.Attributes.Add(new HNMO.Persistence.BasicModel.MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100 });
        var npc = new HNMO.Persistence.BasicModel.MonsterDefinition { Id = Guid.NewGuid(), Number = 2, ObjectKind = NpcObjectKind.PassiveNpc };
        mapDef.MonsterSpawns.Add(CreateSpawn(mapDef, monster, monsterQuantity));
        if (npcQuantity > 0)
        {
            mapDef.MonsterSpawns.Add(CreateSpawn(mapDef, npc, npcQuantity));
        }

        if (amountConfiguration is not null)
        {
            gameConfig.PlugInConfigurations.Add(amountConfiguration);
        }

        var mapInitializer = new MapInitializer(gameConfig, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var plugInConfigurations = amountConfiguration is null ? new List<PlugInConfiguration>() : new List<PlugInConfiguration> { amountConfiguration };
        var plugInManager = new PlugInManager(plugInConfigurations, new NullLoggerFactory(), null, null);
        var gameContext = new GameContext(gameConfig, contextProvider, mapInitializer, new NullLoggerFactory(), plugInManager, NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;
        return (gameContext, mapInitializer);
    }

    private static HNMO.Persistence.BasicModel.MonsterSpawnArea CreateSpawn(GameMapDefinition map, MonsterDefinition definition, int quantity)
    {
        return new HNMO.Persistence.BasicModel.MonsterSpawnArea
        {
            GameMap = map,
            MonsterDefinition = definition,
            SpawnTrigger = SpawnTrigger.Automatic,
            X1 = (byte)(Center.X - 5),
            Y1 = (byte)(Center.Y - 5),
            X2 = (byte)(Center.X + 5),
            Y2 = (byte)(Center.Y + 5),
            Quantity = (short)quantity,
        };
    }
}
