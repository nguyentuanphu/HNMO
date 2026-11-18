// <copyright file="VipServersRateAndJoinTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using Moq;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Persistence.InMemory;
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
            var serverCtx = new Mock<IGameServerContext>();
            serverCtx.SetupGet(c => c.Id).Returns(testServerId);
            serverCtx.SetupGet(c => c.Configuration).Returns(config);
            serverCtx.SetupGet(c => c.PlugInManager).Returns(new HNMO.PlugIns.PlugInManager([], Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, null, null));

            var player = await PlayerTestHelper.CreatePlayerAsync(serverCtx.Object);
            var moneyGroup = new Basic.ItemDropItemGroup
            {
                ItemType = SpecialItemType.Money,
                Chance = 1.0,
                MoneyAmount = 100,
            };
            var monster = new Basic.MonsterDefinition
            {
                Number = 1,
                Designation = "Spider",
                NumberOfMaximumItemDrops = 1,
            };
            monster.DropItemGroups.Add(moneyGroup);

            var randomizer = new Mock<IRandomizer>();
            randomizer.Setup(r => r.NextDouble()).Returns(0.0);
            randomizer.Setup(r => r.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns<int, int>((min, _) => min);
            randomizer.Setup(r => r.NextRandomBool(It.IsAny<double>())).Returns(true);

            var generator = new DefaultDropGenerator(config, randomizer.Object)
            {
                GameContext = serverCtx.Object,
            };

            var (_, money) = await generator.GenerateItemDropsAsync(monster, 100, player).ConfigureAwait(false);
            Assert.That(money, Is.EqualTo(400u));
        }
        finally
        {
            rateStore.SetRates(testServerId, prev.DropRate, prev.ZenRate);
        }
    }
}
