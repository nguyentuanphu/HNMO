// <copyright file="MiniGameKillMoneyTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Runtime.InteropServices;
using System.Threading;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using BasicModel = HNMO.Persistence.BasicModel;
using MonsterAttribute = HNMO.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = HNMO.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests the zen of monsters which are killed inside a mini game.
/// </summary>
[TestFixture]
public class MiniGameKillMoneyTest
{
    private readonly List<MiniGameContext> _games = new();

    /// <summary>
    /// Disposes the created games.
    /// </summary>
    [TearDown]
    public async ValueTask TearDownAsync()
    {
        foreach (var game in this._games)
        {
            await game.DisposeAsync().ConfigureAwait(false);
        }

        this._games.Clear();
    }

    /// <summary>
    /// Like in classic MU, the monsters of Devil Square and Chaos Castle give no zen. Blood Castle keeps its zen,
    /// which is paid directly instead of being dropped.
    /// </summary>
    /// <param name="miniGameType">The type of the mini game in which the monster is killed.</param>
    /// <param name="expectedMoney">The money which the killer receives.</param>
    [TestCase(MiniGameType.DevilSquare, 0)]
    [TestCase(MiniGameType.ChaosCastle, 0)]
    [TestCase(MiniGameType.BloodCastle, 1000)]
    public async Task KilledMonsterGivesZenOnlyInMiniGamesWhichHaveItAsync(MiniGameType miniGameType, int expectedMoney)
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        gameContext.Configuration.GlobalBaseAttributeValues.Add(new BasicModel.ConstValueAttribute(1, Stats.MoneyAmountRate, HNMO.AttributeSystem.AggregateType.AddRaw));
        var plugIn = new RecordingPlugIn();
        gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<IMonsterItemDroppedPlugIn>(plugIn);
        var item = new BasicModel.Item { Definition = new BasicModel.ItemDefinition() };
        var dropGenerator = new Mock<IDropGenerator>();
        dropGenerator.Setup(g => g.GenerateItemDropsAsync(It.IsAny<DataModel.Configuration.MonsterDefinition>(), It.IsAny<int>(), It.IsAny<Player>()))
            .Returns(ValueTask.FromResult<(IEnumerable<Item> Items, uint? Money)>((new[] { item }, 1000u)));
        var killer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        killer.Money = 0;
        killer.CurrentMiniGame = this.CreateMiniGame(killer, miniGameType);
        var monster = await CreateMonsterAsync(gameContext, dropGenerator.Object).ConfigureAwait(false);

        await monster.ReflectDamageAsync(killer, 1_000_000).ConfigureAwait(false);

        // The drop happens delayed, after the death animation. The money is handled before the item is reported.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (plugIn.Drops == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        Assert.That(plugIn.Drops, Is.EqualTo(1));
        Assert.That(killer.Money, Is.EqualTo(expectedMoney));
    }

    private static async ValueTask<Monster> CreateMonsterAsync(IGameContext gameContext, IDropGenerator dropGenerator)
    {
        // A unique id, because the attributes of monster definitions are cached by their id.
        var monsterDefinition = new MonsterDefinition { Id = Guid.NewGuid(), ObjectKind = NpcObjectKind.Monster };
        monsterDefinition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = 100 });
        var map = await gameContext.GetMapAsync(0).ConfigureAwait(false);
        var spawnArea = new MonsterSpawnArea
        {
            MonsterDefinition = monsterDefinition,
            GameMap = map!.Definition,
            X1 = 100,
            Y1 = 100,
            X2 = 100,
            Y2 = 100,
            Quantity = 1,
        };

        var monster = new Monster(
            spawnArea,
            monsterDefinition,
            map,
            dropGenerator,
            new Mock<INpcIntelligence>().Object,
            gameContext.PlugInManager,
            gameContext.PathFinderPool);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        return monster;
    }

    private MiniGameContext CreateMiniGame(Player player, MiniGameType type)
    {
        var mapDefinition = new BasicModel.GameMapDefinition { Number = 9 };
        var definitionMock = new Mock<MiniGameDefinition>();
        definitionMock.SetupGet(d => d.Rewards).Returns(new List<MiniGameReward>());
        definitionMock.SetupGet(d => d.SpawnWaves).Returns(new List<MiniGameSpawnWave>());
        definitionMock.SetupGet(d => d.Entrance).Returns(new ExitGate { Map = mapDefinition });
        var definition = definitionMock.Object;
        definition.Type = type;
        definition.EnterDuration = TimeSpan.FromMinutes(1);
        definition.GameDuration = TimeSpan.FromMinutes(5);
        definition.ExitDuration = TimeSpan.FromMinutes(1);

        var mapInitializerMock = new Mock<IMapInitializer>();
        mapInitializerMock.Setup(m => m.CreateGameMap(It.IsAny<GameMapDefinition>()))
            .Returns<GameMapDefinition>(map => new GameMap(map, TimeSpan.FromMinutes(1), 16));
        var context = new MiniGameContext(new MiniGameMapKey(mapDefinition.Number, definition.GameLevel, string.Empty), definition, player.GameContext, mapInitializerMock.Object);
        this._games.Add(context);
        return context;
    }

    [Guid("0B6A2D57-2E64-4C55-9E64-6D0C7E2E1F31")]
    private sealed class RecordingPlugIn : IMonsterItemDroppedPlugIn
    {
        private int _drops;

        public int Drops => Volatile.Read(ref this._drops);

        public ValueTask MonsterItemDroppedAsync(AttackableNpcBase monster, Player killer, DroppedItem droppedItem)
        {
            Interlocked.Increment(ref this._drops);
            return ValueTask.CompletedTask;
        }
    }
}
