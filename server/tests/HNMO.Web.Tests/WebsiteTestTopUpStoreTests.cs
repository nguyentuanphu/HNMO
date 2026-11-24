// <copyright file="WebsiteTestTopUpStoreTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests;

using System.IO;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Services;
using Moq;

/// <summary>Tests test-only orders against the unified host state store.</summary>
[TestFixture]
public sealed class WebsiteTestTopUpStoreTests
{
    private static readonly TestCoinPackage Package = new("fictional", "Fictional package", "Gói hư cấu", 25_000, 10, CashShopCoinType.WCoinC);

    /// <summary>Checks isolation, retry, immutable amount, and the Sydney-day limit.</summary>
    [Test]
    public void OnlyDemoCreatesOrdersAndRetriesKeepTheirOriginalAmount()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var state = HnmoStateStore.Open(Path.Combine(directory.FullName, HnmoStateStore.StateFileName))!;
            var now = new Clock(new DateTimeOffset(2026, 11, 24, 12, 59, 0, TimeSpan.Zero));
            var demo = new WebsiteTestTopUpStore(state, new AdminAuditSettings { IsDemo = true }, now);
            var live = new WebsiteTestTopUpStore(state);
            var owner = Guid.NewGuid();
            var first = Guid.NewGuid();
            var settings = new TestTopUpSettings(true, 1, [Package]);
            demo.SaveSettings(settings);
            Assert.That(live.PublicSettings.Packages, Is.Empty);
            Assert.That(live.TryCreate(owner, first, Package.Id), Is.Null);
            var created = demo.TryCreate(owner, first, Package.Id);
            Assert.That(created, Is.Not.Null);
            Assert.That(demo.TryCreate(owner, Guid.NewGuid(), Package.Id), Is.Null);
            Assert.That(demo.TryCreate(Guid.NewGuid(), first, Package.Id), Is.Null);

            demo.SaveSettings(settings with { Packages = [Package with { PriceVnd = 30_000 }] });
            Assert.That(demo.TryCreate(owner, first, Package.Id)?.Package.PriceVnd, Is.EqualTo(25_000));
            now.Utc = now.Utc.AddMinutes(2);
            Assert.That(demo.TryCreate(owner, Guid.NewGuid(), Package.Id)?.Package.PriceVnd, Is.EqualTo(30_000));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    /// <summary>Rejects mismatched transfers and grants the paid package once on replay.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ConfirmationChecksTransferAndGrantsExactlyOnceAsync()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var state = HnmoStateStore.Open(Path.Combine(directory.FullName, HnmoStateStore.StateFileName))!;
            HnmoPlayerStateRowStoreRegistry.RegisterBackend(state.FilePath, new InMemoryHnmoPlayerStateRowBackend());
            var store = new WebsiteTestTopUpStore(state, new AdminAuditSettings { IsDemo = true });
            store.SaveSettings(new TestTopUpSettings(true, 2, [Package]));
            var database = new InMemoryPersistenceContextProvider();
            var game = new GameConfiguration();
            var config = new Mock<IDataSource<GameConfiguration>>();
            config.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
            Guid accountId;
            using (var context = database.CreateNewPlayerContext(game))
            {
                var player = context.CreateNew<Account>();
                player.LoginName = "buyer";
                accountId = player.GetId();
                await context.SaveChangesAsync().ConfigureAwait(false);
            }

            var wallet = new CashShopCoinService(database, config.Object);
            var service = new WebsiteTestBankConfirmation(store, database, config.Object, wallet);
            var order = store.TryCreate(accountId, Guid.NewGuid(), Package.Id)!;
            Assert.That(await service.ConfirmAsync(order.Id, "other", Package.PriceVnd).ConfigureAwait(false), Is.False);
            Assert.That(await service.ConfirmAsync(order.Id, order.Reference, Package.PriceVnd - 1).ConfigureAwait(false), Is.False);
            Assert.That((await wallet.GetSummaryAsync("buyer").ConfigureAwait(false))?.Grants, Is.Empty);
            var replay = await Task.WhenAll(
                service.ConfirmAsync(order.Id, order.Reference, Package.PriceVnd),
                service.ConfirmAsync(order.Id, order.Reference, Package.PriceVnd)).ConfigureAwait(false);
            Assert.That(replay, Is.EqualTo(new[] { true, true }));
            var grants = (await wallet.GetSummaryAsync("buyer").ConfigureAwait(false))!.Grants;
            Assert.That(grants.Select(g => g.Amount), Is.EqualTo(new[] { Package.Coins }));
            Assert.That(ActivityLogStore.Open(state.FilePath).Query().Count, Is.EqualTo(1));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private sealed class Clock(DateTimeOffset utc) : TimeProvider
    {
        public DateTimeOffset Utc { get; set; } = utc;

        public override DateTimeOffset GetUtcNow() => this.Utc;
    }
}
