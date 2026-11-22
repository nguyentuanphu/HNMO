// <copyright file="MayaBodyNonAttackableTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.Persistence.Initialization.VersionSeasonSix;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Tests;

/// <summary>
/// Verifies that Maya's body (monster #364 in Kanturu Event) is non-attackable in data and at runtime.
/// </summary>
[TestFixture]
public class MayaBodyNonAttackableTest
{
    private const byte KanturuEventMapNumber = 39;
    private const short MayaBodyNumber = 364;

    /// <summary>
    /// Verifies Maya's body is defined as <see cref="NpcObjectKind.PassiveNpc"/> with 0 drops
    /// and cannot be attacked when Wave 0 spawns on Kanturu Event.
    /// </summary>
    [Test]
    public async ValueTask MayaBodyCannotBeAttackedWhenSpawnedAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(contextProvider, new NullLoggerFactory()).CreateInitialDataAsync(0, false).ConfigureAwait(false);
        var gameConfiguration = (await contextProvider.CreateNewConfigurationContext().GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        var mayaDefinition = gameConfiguration.Monsters.Single(m => m.Number == MayaBodyNumber);
        Assert.That(mayaDefinition.ObjectKind, Is.EqualTo(NpcObjectKind.PassiveNpc));
        Assert.That(mayaDefinition.NumberOfMaximumItemDrops, Is.EqualTo(0));

        var mapInitializer = new MapInitializer(gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(gameConfiguration, contextProvider, mapInitializer, new NullLoggerFactory(), new PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;

        var map = await gameContext.GetMapAsync(KanturuEventMapNumber).ConfigureAwait(false);
        await mapInitializer.InitializeNpcsOnWaveStartAsync(map!, NoOpEventStateProvider.Instance, 0).ConfigureAwait(false);

        var attackables = map!.GetAttackablesInRange(new(188, 110), 15).ToList();
        Assert.That(attackables, Is.Empty);
    }

    private sealed class NoOpEventStateProvider : IEventStateProvider
    {
        public static NoOpEventStateProvider Instance { get; } = new();

        public bool IsEventRunning => true;

        public bool IsSpawnWaveActive(byte waveNumber) => waveNumber == 0;
    }
}
