// <copyright file="VipServersRateAndJoinTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Basic = HNMO.Persistence.BasicModel;

/// <summary>
/// Tests for per-server Drop/Zen rates (<see cref="GameServerRateStore"/>), <see cref="DefaultDropGenerator"/> scaling,
/// and VIP-only <see cref="Stats.IsVip"/> server requirement detection.
/// </summary>
[TestFixture]
public class VipServersRateAndJoinTests
{
    /// <summary>Verifies <see cref="GameServerRateStore"/> clamps, persists, and resolves per-server multipliers.</summary>
    [Test]
    public void StoreClampsPersistsAndReloadsRates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hnmo-server-rates-{Guid.NewGuid():N}.json");
        try
        {
            var store = new GameServerRateStore(path);
            Assert.That(store.GetRates(2), Is.EqualTo((1.0f, 1.0f)));

            store.SetRates(2, 2.5f, 3.25f);
            store.SetRates(4, 200f, 0.01f);

            var reloaded = new GameServerRateStore(path);
            Assert.That(reloaded.GetRates(2), Is.EqualTo((2.5f, 3.25f)));
            Assert.That(reloaded.GetRates(4), Is.EqualTo((50.0f, 0.1f)));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Verifies <see cref="GameServerRateStore.IsVipOnly(GameServerDefinition?)"/> detects <see cref="Stats.IsVip"/>.</summary>
    [Test]
    public void IsVipOnlyMatchesStatsIsVipRequirement()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var ctx = provider.CreateNewContext();
        var def = ctx.CreateNew<GameServerDefinition>();
        def.ServerID = 1;
        def.Description = "VIP Server";
        Assert.That(GameServerRateStore.IsVipOnly(def), Is.False);

        var req = ctx.CreateNew<AttributeRequirement>();
        req.Attribute = Stats.IsVip;
        req.MinimumValue = 1;
        def.JoinRequirements.Add(req);

        Assert.That(GameServerRateStore.IsVipOnly(def), Is.True);
    }

    /// <summary>Verifies <see cref="DefaultDropGenerator"/> applies per-server Zen rate multiplier.</summary>
    /// <returns>Async test task.</returns>
    [Test]
    public async Task DefaultDropGeneratorAppliesServerZenRate()
    {
        const byte testServerId = 77;
        var rateStore = GameServerRateStore.OpenDefault();
        var prev = rateStore.GetRates(testServerId);
        try
        {
            rateStore.SetRates(testServerId, 1.0f, 4.0f);

            var config = PlayerTestHelper.CreateGameConfiguration();
            var mapInitializer = new MapInitializer(config, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
            var serverCtx = new TestGameServerContext(
                testServerId,
                config,
                new InMemoryPersistenceContextProvider(),
                mapInitializer);
            mapInitializer.PlugInManager = serverCtx.PlugInManager;
            mapInitializer.PathFinderPool = serverCtx.PathFinderPool;

            var player = await PlayerTestHelper.CreatePlayerAsync(serverCtx);
            var moneyGroup = new Basic.DropItemGroup
            {
                ItemType = SpecialItemType.Money,
                Chance = 1.0,
            };
            var monster = new Basic.MonsterDefinition
            {
                Number = 1,
                Designation = "Spider",
                NumberOfMaximumItemDrops = 1,
            };
            monster.DropItemGroups.Add(moneyGroup);
            monster.Attributes.Add(new Basic.MonsterAttribute { AttributeDefinition = Stats.Level, Value = 10 });

            var randomizer = new Mock<IRandomizer>();
            randomizer.Setup(r => r.NextDouble()).Returns(0.0);
            randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns<int, int>((min, _) => min);
            randomizer.Setup(r => r.NextRandomBool(It.IsAny<double>())).Returns(true);

            var generator = new DefaultDropGenerator(config, randomizer.Object)
            {
                GameContext = serverCtx,
            };

            rateStore.SetRates(testServerId, 1.0f, 1.0f);
            var (_, moneyBase) = await generator.GenerateItemDropsAsync(monster, 100, player).ConfigureAwait(false);
            rateStore.SetRates(testServerId, 1.0f, 4.0f);
            var (_, moneyScaled) = await generator.GenerateItemDropsAsync(monster, 100, player).ConfigureAwait(false);
            Assert.That(moneyBase, Is.GreaterThan(0u));
            Assert.That(moneyScaled, Is.EqualTo(moneyBase!.Value * 4u));
        }
        finally
        {
            rateStore.SetRates(testServerId, prev.DropRate, prev.ZenRate);
        }
    }

    private sealed class TestGameServerContext(
        byte id,
        GameConfiguration configuration,
        IPersistenceContextProvider persistence,
        IMapInitializer mapInitializer)
        : GameContext(
            configuration,
            persistence,
            mapInitializer,
            NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator()),
          IGameServerContext
    {
        public event EventHandler<GuildEventArgs>? GuildDeleted
        {
            add { }
            remove { }
        }

        public event EventHandler<GuildEventArgs>? GuildChanged
        {
            add { }
            remove { }
        }

        public byte Id => id;

        public IGuildServer GuildServer => Mock.Of<IGuildServer>();

        public ILoginServer LoginServer => Mock.Of<ILoginServer>();

        public IFriendServer FriendServer => Mock.Of<IFriendServer>();

        public IEventPublisher EventPublisher => Mock.Of<IEventPublisher>();

        public GameServerConfiguration ServerConfiguration { get; } = new();

        public IMapHostLocator? MapHostLocator => null;

        public IEnumerable<AttributeRequirement> JoinRequirements => [];

        public new void StopPeriodicTasks()
        {
        }

        public ValueTask RefreshGuildInfoAsync(uint guildId) => ValueTask.CompletedTask;

        public ValueTask ForEachGuildPlayerAsync(uint guildId, Func<Player, Task> action) => ValueTask.CompletedTask;

        public ValueTask ForEachAlliancePlayerAsync(uint guildId, Func<Player, Task> action) => ValueTask.CompletedTask;

        public ValueTask RegisterGuildMemberAsync(Player guildMember) => ValueTask.CompletedTask;

        public ValueTask UnregisterGuildMemberAsync(Player guildMember) => ValueTask.CompletedTask;

        public ValueTask RemoveGuildAsync(uint guildId) => ValueTask.CompletedTask;

        public void UpdateGuildHostility(uint guildIdA, IReadOnlyList<uint> allianceGuildIdsA, uint guildIdB, IReadOnlyList<uint> allianceGuildIdsB, bool created)
        {
        }

        public bool AreGuildsRival(uint guild1Id, uint guild2Id) => false;
    }
}
