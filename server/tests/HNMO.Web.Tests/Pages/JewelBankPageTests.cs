// <copyright file="JewelBankPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.IO;
using System.Threading;
using Bunit;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Services;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Shared.Services;
using HNMO.Web.Tests.AdminAuth;
using HNMO.Web.Tests.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// bUnit tests for <see cref="JewelBank"/> page: database receipt history (<see cref="AssetOperation"/>)
/// and JSON-to-database dry-run report + manual import execution.
/// </summary>
[TestFixture]
[NonParallelizable]
public class JewelBankPageTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates a temp directory for the legacy JSON state file.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-jb-page-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes the temp directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the page shows database <see cref="AssetOperation"/> receipts instead of the JSON log,
    /// displays the dry-run import report, and imports entries when clicking the import button.
    /// </summary>
    [Test]
    public async Task ShowsDatabaseReceiptsDryRunReportAndRunsImportAsync()
    {
        var jsonPath = Path.Combine(this._tempDir, "jewelbank.json");
        var provider = new InMemoryPersistenceContextProvider();

        GameConfiguration gameConfig;
        Guid aliceId;
        using (var setup = provider.CreateNewContext())
        {
            gameConfig = setup.CreateNew<GameConfiguration>();
            var cfg = new JewelBankConfiguration { FileName = jsonPath, AllowWebsite = true };
            var pluginCfg = setup.CreateNew<PlugInConfiguration>();
            pluginCfg.TypeId = typeof(JewelBankPlugIn).GUID;
            pluginCfg.IsActive = true;
            pluginCfg.SetConfiguration(cfg, null);
            await setup.SaveChangesAsync().ConfigureAwait(false);
        }

        using (var players = provider.CreateNewPlayerContext(gameConfig))
        {
            var alice = players.CreateNew<Account>();
            alice.LoginName = "alice";
            aliceId = alice.GetId();
            var seedOp = new AssetOperationRequest(
                Guid.NewGuid(),
                aliceId,
                "jewel-deposit",
                "seed-hash",
                "game",
                "deposit|bless:2",
                [new JewelBankDelta("bless", 2, 100)]);
            await players.SaveChangesWithAssetOperationAsync(seedOp).ConfigureAwait(false);
        }

        // Seed legacy JSON with both a stale JSON log entry (must not appear) and an unimported soul balance + pending withdrawal.
        var store = JewelBankStore.Open(jsonPath);
        store.Mutate<bool>(data =>
        {
            data.Accounts["alice"] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["soul"] = 5 };
            data.PendingWithdrawals.Add(new JewelBankPendingWithdrawal
            {
                Id = "w1",
                LoginName = "alice",
                JewelKey = "chaos",
                Quantity = 3,
                CreatedAtUtc = DateTime.UtcNow,
            });
            data.Log.Add(new JewelBankLogEntry
            {
                AtUtc = DateTime.UtcNow,
                LoginName = "json-only-ghost",
                Action = "json-stale",
                Detail = "should-not-display",
                Amount = 99,
                Source = "json",
            });
            return (true, true);
        });

        await using var ui = new BunitContext();
        ui.Services.AddLogging();
        ui.Services.AddToasts();
        ui.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
        ui.Services.AddAdminPanelAuth(ui.Services.BuildServiceProvider().GetRequiredService<IConfiguration>());
        ui.AddAdministratorAuthorization();
        ui.Services.AddSingleton<IPersistenceContextProvider>(provider);
        var migratable = new Mock<IMigratableDatabaseContextProvider>();
        migratable.Setup(m => m.RepositoryProvider).Returns(provider.RepositoryProvider);
        ui.Services.AddSingleton(migratable.Object);
        var gameSource = new Mock<IDataSource<GameConfiguration>>();
        gameSource.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(gameConfig);
        ui.Services.AddSingleton(gameSource.Object);
        ui.Services.AddScoped<AdminOperationExecutor>(_ => AdminOperationTestServices.CreateExecutor());
        ui.Services.AddScoped<IAdminOperationContext, UnconfiguredAdminOperationContext>();
        ui.Services.AddScoped<JewelBankOperations>();
        ui.Services.AddSingleton(new AdminWriteSettings { ReadOnly = false });
        ui.Services.AddSingleton(new AdminAuditSettings());
        ui.Services.AddSingleton(new AdminLogAccess { LocalFilesAvailable = false });
        ui.Services.AddSingleton(new AdminChangeConfirmation(TimeProvider.System));
        ui.Services.AddSingleton<IAdminAuditRepository>(new InMemoryAdminAuditRepository());
        ui.Services.AddSingleton<IConfigurationChangePublisher>(IConfigurationChangePublisher.None);
        ui.Services.AddSingleton(new PlugInManager(Array.Empty<PlugInConfiguration>(), NullLoggerFactory.Instance, null, null));
        ui.Services.AddSingleton<SetupService>();
        ui.Services.AddSingleton<ConfigurationSearchIndexCache>();
        ui.Services.AddScoped<PluginWriteOperations>();
        ui.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();

        var cut = ui.Render<JewelBank>();
        cut.WaitForState(() => cut.FindAll("#jb-receipts, #jb-log").Count > 0 && cut.FindAll("#jb-run-import").Count > 0, TimeSpan.FromSeconds(5));

        var logText = cut.Find("#jb-log").TextContent;
        Assert.That(logText, Does.Contain("deposit|bless:2"));
        Assert.That(logText, Does.Contain("alice"));
        Assert.That(logText, Does.Not.Contain("json-only-ghost"));

        var importText = cut.Find("#jb-import").TextContent;
        Assert.That(importText, Does.Contain("soul: 5"));
        Assert.That(importText, Does.Contain("alice soul 5"));

        // Dry run must have written nothing before clicking the button.
        using (var checkBefore = provider.CreateNewPlayerContext(gameConfig))
        {
            var balances = await checkBefore.GetJewelBankBalancesAsync(aliceId).ConfigureAwait(false);
            Assert.That(balances.ContainsKey("soul"), Is.False);
        }

        await cut.Find("#jb-run-import").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForState(() => cut.Find("#jb-log").TextContent.Contains("import-balance|soul:5", StringComparison.Ordinal), TimeSpan.FromSeconds(5));

        using (var checkAfter = provider.CreateNewPlayerContext(gameConfig))
        {
            var balances = await checkAfter.GetJewelBankBalancesAsync(aliceId).ConfigureAwait(false);
            Assert.That(balances["soul"], Is.EqualTo(5));
        }
    }
}
