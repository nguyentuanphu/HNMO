// <copyright file="EconomyDashboardPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Economy;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;
using Account = HNMO.DataModel.Entities.Account;
using ItemStorage = HNMO.DataModel.Entities.ItemStorage;

/// <summary>
/// bUnit tests for the admin panel Economy Dashboard page (<c>/economy-dashboard</c>).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class EconomyDashboardPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private string _file = null!;

    /// <summary>Sets up bUnit context and two days of snapshots including a flagged anomaly.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        EconomyStore.ResetForTests();
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-econ-page-{Guid.NewGuid():N}.json");

        var provider = new InMemoryPersistenceContextProvider();
        GameConfiguration config;
        using (var setup = provider.CreateNewContext())
        {
            config = setup.CreateNew<GameConfiguration>();
            this._plugin = setup.CreateNew<PlugInConfiguration>();
            this._plugin.TypeId = typeof(EconomyDashboardPlugIn).GUID;
            this._plugin.IsActive = true;
            this._plugin.CustomConfiguration = JsonSerializer.Serialize(new EconomyConfiguration
            {
                FileName = this._file,
                AnomalyJumpPercentThreshold = 50,
                AnomalyMinZenDelta = 10_000_000,
                AnomalyMinJewelDelta = 25,
                AnomalyMinCoinDelta = 500,
            });
            config.PlugInConfigurations.Add(this._plugin);
            await setup.SaveChangesAsync().ConfigureAwait(false);

            using var pctx = provider.CreateNewPlayerContext(config);
            var alice = pctx.CreateNew<Account>();
            alice.LoginName = "alice";
            alice.Vault = pctx.CreateNew<ItemStorage>();
            alice.Vault.Money = 20_000_000;
            await pctx.SaveChangesAsync().ConfigureAwait(false);
        }

        var econCfg = JsonSerializer.Deserialize<EconomyConfiguration>(this._plugin.CustomConfiguration!)!;
        var store = EconomyStore.Open(this._file);
        store.SaveSnapshot(
            new EconomyDailySnapshot
            {
                DateSydney = "2026-11-15",
                CapturedAtUtc = new DateTime(2026, 11, 15, 2, 0, 0, DateTimeKind.Utc),
                TotalZen = 1_000_000,
                TotalJewels = 10,
                AccountSamples = [new EconomyAccountTotals { AccountLogin = "alice", Zen = 1_000_000 }],
            },
            econCfg);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var manager = new PlugInManager([], Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IFeaturePlugIn, EconomyDashboardPlugIn>();
        var executor = AdminOperationTestServices.CreateExecutor();
        var writes = new PluginWriteOperations(provider, source.Object, manager, executor, new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });
        var econOps = new EconomyDashboardOperations(provider, source.Object, executor, TimeProvider.System);

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(econOps);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<EconomyDashboard>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Cleans up bUnit and temporary state.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        EconomyStore.ResetForTests();
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }
    }

    /// <summary>Verifies SVG chart, help toggle, live snapshot capture, anomaly link to /activity-log, and save.</summary>
    [Test]
    public void RendersChartCapturesSnapshotFlagsAnomalyAndLinksActivityLog()
    {
        var cut = this._context.Render<EconomyDashboard>();
        cut.WaitForAssertion(() => Assert.That(cut.Find("#ed-trend-svg"), Is.Not.Null), LoadTimeout);

        cut.Find("#ed-help-btn").Click();
        Assert.That(cut.Find("#economy-dashboard-help").TextContent, Contains.Substring("Jewel Bank"));

        cut.Find("#ed-capture").Click();
        cut.WaitForAssertion(() => Assert.That(cut.Find("#ed-anomalies-table").TextContent, Contains.Substring("alice")), LoadTimeout);
        Assert.That(cut.Markup, Contains.Substring("/activity-log?account=alice"));

        cut.Find("#ed-pct").Change("75");
        cut.Find("#ed-save").Click();
        cut.WaitForAssertion(() => Assert.That(this.Saved()?.AnomalyJumpPercentThreshold, Is.EqualTo(75)), LoadTimeout);
    }

    /// <summary>Verifies Vietnamese localization renders properly.</summary>
    [Test]
    public void RendersVietnameseLabels()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var cut = this._context.Render<EconomyDashboard>();
        cut.WaitForAssertion(() => Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Bảng điều khiển Kinh tế")), LoadTimeout);
    }

    private EconomyConfiguration? Saved() =>
        this._plugin.CustomConfiguration is { } json ? JsonSerializer.Deserialize<EconomyConfiguration>(json) : null;
}
