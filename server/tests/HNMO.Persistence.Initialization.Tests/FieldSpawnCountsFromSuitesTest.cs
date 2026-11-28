// <copyright file="FieldSpawnCountsFromSuitesTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.PlugIns;
using HNMO.Persistence.Initialization.Updates;
using HNMO.Persistence.InMemory;

/// <summary>
/// The field maps have the monster counts of the Season 6/9 server suites' spawn table.
/// </summary>
[TestFixture]
internal class FieldSpawnCountsFromSuitesTest
{
    /// <summary>
    /// The map, monster and count of the suites for each changed field monster.
    /// </summary>
    private static readonly (byte Map, short Monster, int Count)[] SuiteCounts =
    [
        (1, 8, 21), // 20 before
        (1, 14, 29), // 33 before
        (4, 39, 89), // 87 before
        (4, 40, 65), // 71 before
        (7, 51, 66), // 65 before
        (10, 69, 15), // 11 before
        (10, 70, 13), // 6 before
        (10, 71, 13), // 10 before
        (10, 73, 5), // 6 before
        (10, 75, 15), // 12 before
        (26, 187, 8), // 9 before
        (26, 188, 10), // 9 before
        (29, 274, 16), // 10 before
        (31, 293, 42), // 39 before
        (31, 294, 48), // 47 before
        (33, 305, 39), // 38 before
        (33, 306, 40), // 39 before
        (33, 550, 28), // 25 before
        (34, 313, 25), // 24 before
        (36, 337, 16), // 10 before
        (41, 409, 11), // 10 before
        (41, 411, 7), // 9 before
    ];

    /// <summary>A new database has the suites' counts.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheSuiteCountsAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        AssertSuiteCounts(gameConfiguration);
    }

    /// <summary>A new database has no spawn with the number of a removed spot, and every added spot.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NewDatabaseHasTheChangedSpotsAsync()
    {
        var gameConfiguration = await CreateConfigurationAsync(new InMemoryPersistenceContextProvider()).ConfigureAwait(false);

        var wrong = FieldSpawnCountsFromSuitesUpdatePlugIn.Changes
            .Where(change => Spawns(gameConfiguration, change.Map).Any(s => s.GetId() == change.Id) != change.Added)
            .Select(change => $"{change.Map}/{change.Number}")
            .ToList();
        Assert.That(wrong, Is.Empty);
    }

    /// <summary>An old save gets the suites' counts; applying the update twice changes nothing more.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateChangesAnOldSaveAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        MakeOldSave(context, gameConfiguration);

        var update = new FieldSpawnCountsFromSuitesUpdatePlugIn();
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);
        await update.ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        AssertSuiteCounts(gameConfiguration);
    }

    /// <summary>A spot which the host moved stays.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task MovedSpotIsKeptAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var gameConfiguration = await CreateConfigurationAsync(contextProvider).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        MakeOldSave(context, gameConfiguration);
        var removed = FieldSpawnCountsFromSuitesUpdatePlugIn.Changes.First(c => !c.Added);
        var moved = Spawns(gameConfiguration, removed.Map).Single(s => s.GetId() == removed.Id);
        moved.X1 = moved.X2 = (byte)(moved.X1 + 1);

        await new FieldSpawnCountsFromSuitesUpdatePlugIn().ApplyUpdateAsync(context, gameConfiguration).ConfigureAwait(false);

        Assert.That(Spawns(gameConfiguration, removed.Map), Does.Contain(moved));
    }

    /// <summary>Version two repairs blocked unmodified spots and preserves host changes.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UpdateMovesOnlyUnmodifiedBlockedAtlansSpawnsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        var configuration = await CreateConfigurationAsync(provider).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var map = configuration.Maps.Single(map => map.Number == 7 && map.Discriminator == 0);
        var update = new FieldSpawnCountsFromSuitesUpdatePlugIn();
        var first = map.MonsterSpawns.Single(spawn => spawn.GetId() == FieldSpawnCountsFromSuitesUpdatePlugIn.Changes.Single(change => change.Map == 7 && change.Number == 438).Id);
        var second = map.MonsterSpawns.Single(spawn => spawn.GetId() == FieldSpawnCountsFromSuitesUpdatePlugIn.Changes.Single(change => change.Map == 7 && change.Number == 439).Id);
        byte TerrainAt(MonsterSpawnArea spawn) => map.TerrainData![3 + (spawn.Y1 * 256) + spawn.X1];

        Assert.That(TerrainAt(first), Is.Zero);
        Assert.That(TerrainAt(second), Is.Zero);
        await AssertSpawnsAsync(configuration, provider, map, first, second).ConfigureAwait(false);
        first.X1 = first.X2 = 251;
        second.X1 = second.X2 = 7;
        second.Y1 = second.Y2 = 52;
        first.Direction = Direction.North;
        Assert.That(TerrainAt(first), Is.EqualTo(4));
        Assert.That(TerrainAt(second), Is.EqualTo(4));

        await update.ApplyUpdateAsync(context, configuration).ConfigureAwait(false);

        Assert.That(first.X1, Is.EqualTo(251));
        Assert.That(second.X1, Is.EqualTo(14));
        Assert.That(second.Y1, Is.EqualTo(51));
        Assert.That(TerrainAt(second), Is.Zero);
        await AssertSpawnsAsync(configuration, provider, map, second).ConfigureAwait(false);
    }

    private static async Task AssertSpawnsAsync(
        GameConfiguration configuration,
        InMemoryPersistenceContextProvider provider,
        GameMapDefinition definition,
        params MonsterSpawnArea[] spawns)
    {
        var initializer = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var plugIns = new PlugInManager(null, NullLoggerFactory.Instance, null, null);
        var game = new GameContext(configuration, provider, initializer, NullLoggerFactory.Instance, plugIns, NullDropGenerator.Instance, new ConfigurationChangeMediator());
        initializer.PlugInManager = game.PlugInManager;
        initializer.PathFinderPool = game.PathFinderPool;
        var map = initializer.CreateGameMap(definition);
        try
        {
            foreach (var spawn in spawns)
            {
                var monster = await initializer.InitializeSpawnAsync(0, map, spawn).ConfigureAwait(false);
                Assert.That(monster, Is.Not.Null, $"Great Bahamut failed to spawn at ({spawn.X1}, {spawn.Y1}).");
            }
        }
        finally
        {
            await map.TearDownAsync().ConfigureAwait(false);
        }
    }

    private static void MakeOldSave(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var change in FieldSpawnCountsFromSuitesUpdatePlugIn.Changes)
        {
            var map = gameConfiguration.Maps.Single(m => m.Number == change.Map && m.Discriminator == 0);
            if (change.Added)
            {
                map.MonsterSpawns.Remove(map.MonsterSpawns.Single(s => s.GetId() == change.Id));
                continue;
            }

            var spawn = context.CreateNew<MonsterSpawnArea>();
            spawn.SetGuid(change.Map, change.Number);
            spawn.GameMap = map;
            spawn.MonsterDefinition = gameConfiguration.Monsters.Single(m => m.Number == change.Monster);
            spawn.X1 = change.X1;
            spawn.Y1 = change.Y1;
            spawn.X2 = change.X2;
            spawn.Y2 = change.Y2;
            spawn.Quantity = change.Quantity;
            spawn.SpawnTrigger = SpawnTrigger.Automatic;
            map.MonsterSpawns.Add(spawn);
        }
    }

    private static void AssertSuiteCounts(GameConfiguration gameConfiguration)
    {
        var wrong = SuiteCounts
            .Select(expected => (expected, Count: Spawns(gameConfiguration, expected.Map).Where(s => s.MonsterDefinition?.Number == expected.Monster).Sum(s => s.Quantity)))
            .Where(x => x.Count != x.expected.Count)
            .Select(x => $"map {x.expected.Map} monster {x.expected.Monster}: {x.Count} instead of {x.expected.Count}")
            .ToList();
        Assert.That(wrong, Is.Empty);
    }

    private static ICollection<MonsterSpawnArea> Spawns(GameConfiguration configuration, byte map)
        => configuration.Maps.Single(m => m.Number == map && m.Discriminator == 0).MonsterSpawns;

    private static async Task<GameConfiguration> CreateConfigurationAsync(InMemoryPersistenceContextProvider contextProvider)
    {
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = contextProvider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
    }
}
