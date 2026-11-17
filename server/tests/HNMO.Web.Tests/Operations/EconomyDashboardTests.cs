// <copyright file="EconomyDashboardTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Operations;

using System.IO;
using System.Threading;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Economy;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Tests.Mcp;
using Account = HNMO.DataModel.Entities.Account;
using Character = HNMO.DataModel.Entities.Character;
using ItemStorage = HNMO.DataModel.Entities.ItemStorage;

/// <summary>
/// Unit tests for <see cref="EconomyStore"/> and <see cref="EconomyDashboardOperations"/> (ROADMAP 14.25).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class EconomyDashboardTests
{
    private string _file = null!;
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _config = null!;
    private MutableTimeProvider _clock = null!;

    /// <summary>Sets up seeded accounts, jewels, activity logs, and external provider.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        ActivityLogStore.ResetForTests();
        EconomyStore.ResetForTests();
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-econ-{Guid.NewGuid():N}.json");
        this._clock = new MutableTimeProvider(new DateTimeOffset(2026, 11, 16, 2, 0, 0, TimeSpan.Zero)); // 13:00 AEDT on 2026-11-16

        this._provider = new InMemoryPersistenceContextProvider();
        using var setup = this._provider.CreateNewContext();
        this._config = setup.CreateNew<GameConfiguration>();
        var blessDef = setup.CreateNew<ItemDefinition>();
        blessDef.Name = "Jewel of Bless";
        blessDef.Group = 14;
        blessDef.Number = 13;
        blessDef.Durability = 1;
        this._config.Items.Add(blessDef);

        var bundleDef = setup.CreateNew<ItemDefinition>();
        bundleDef.Name = "Bundled Jewel of Soul";
        bundleDef.Group = 12;
        bundleDef.Number = 31;
        bundleDef.Durability = 1;
        this._config.Items.Add(bundleDef);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        using var pctx = this._provider.CreateNewPlayerContext(this._config);
        var alice = pctx.CreateNew<Account>();
        alice.LoginName = "alice";
        alice.WCoinC = 300;
        alice.Vault = pctx.CreateNew<ItemStorage>();
        alice.Vault.Money = 2_000_000;
        alice.Vault.Items.Add(new HNMO.Persistence.BasicModel.Item { Definition = bundleDef, Level = 1, Durability = 1 }); // 20 jewels

        var aliceChar = pctx.CreateNew<Character>();
        aliceChar.Name = "AliceBM";
        aliceChar.Inventory = pctx.CreateNew<ItemStorage>();
        aliceChar.Inventory.Money = 500_000;
        aliceChar.Inventory.Items.Add(new HNMO.Persistence.BasicModel.Item { Definition = blessDef, Durability = 1 }); // 1 jewel
        alice.Characters.Add(aliceChar);
        await pctx.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>Cleans up temporary state.</summary>
    [TearDown]
    public void TearDown()
    {
        EconomyStore.ResetForTests();
        ActivityLogStore.ResetForTests();
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }
    }

    /// <summary>
    /// Verifies totals aggregation across inventory, vault, and external hooks, daily activity drop/sink
    /// rates from PR 3A, and sudden jump anomaly flagging on day 2.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task CapturesSnapshotsRatesExternalPoolsAndFlagsAnomaliesAsync()
    {
        var ext = new StubExternalProvider(
        [
            new EconomyAccountTotals { AccountLogin = "alice", Zen = 1_000_000, Coins = 200, Jewels = 5 },
        ]);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._config);
        var ops = new EconomyDashboardOperations(
            this._provider,
            source.Object,
            AdminOperationTestServices.CreateExecutor(),
            this._clock,
            [ext]);

        var cfg = new EconomyConfiguration
        {
            FileName = this._file,
            AnomalyJumpPercentThreshold = 50,
            AnomalyMinZenDelta = 5_000_000,
            AnomalyMinJewelDelta = 25,
            AnomalyMinCoinDelta = 400,
        };

        var actStore = ActivityLogStore.Open(this._file);
        actStore.Append(new ActivityLogEntry
        {
            TimestampUtc = this._clock.GetUtcNow().UtcDateTime,
            Kind = ActivityKind.Pickup,
            Account = "alice",
            ItemSummary = "Jewel of Bless",
        });
        actStore.Append(new ActivityLogEntry
        {
            TimestampUtc = this._clock.GetUtcNow().UtcDateTime,
            Kind = ActivityKind.NpcBuy,
            Account = "alice",
            ItemSummary = "Potion",
            ZenDelta = -12000,
        });

        var day1 = await ops.CaptureNowAsync(cfg, default).ConfigureAwait(false);
        Assert.That(day1.TotalZen, Is.EqualTo(3_500_000));
        Assert.That(day1.TotalCoins, Is.EqualTo(500));
        Assert.That(day1.TotalJewels, Is.EqualTo(26));
        Assert.That(day1.DropsPickedPerDay, Is.EqualTo(1));
        Assert.That(day1.NpcBuyZenSinkPerDay, Is.EqualTo(12000));
        Assert.That(day1.Anomalies, Is.Empty);

        // Advance to next Sydney day and inject a sudden jump on alice's vault
        this._clock.SetUtcNow(this._clock.GetUtcNow().AddDays(1));
        using (var pctx = this._provider.CreateNewPlayerContext(this._config))
        {
            var acc = (await pctx.GetAccountsOrderedByLoginNameAsync(0, 10, default).ConfigureAwait(false)).Single();
            acc.Vault!.Money += 25_000_000;
            await pctx.SaveChangesAsync().ConfigureAwait(false);
        }

        var day2 = await ops.CaptureNowAsync(cfg, default).ConfigureAwait(false);
        Assert.That(day2.Anomalies.Any(a => a.Scope == "Account" && a.AccountLogin == "alice" && a.Metric == "Zen"), Is.True);
        Assert.That(day2.Anomalies.Any(a => a.Scope == "Global" && a.Metric == "Zen"), Is.True);
    }

    private sealed class MutableTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _now = initial;

        public override DateTimeOffset GetUtcNow() => this._now;

        public void SetUtcNow(DateTimeOffset next) => this._now = next;
    }

    private sealed class StubExternalProvider(IReadOnlyList<EconomyAccountTotals> totals) : IEconomyExternalAssetProvider
    {
        public ValueTask<IReadOnlyList<EconomyAccountTotals>> GetExternalAssetsAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(totals);
    }
}
