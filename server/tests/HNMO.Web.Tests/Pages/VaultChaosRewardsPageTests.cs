// <copyright file="VaultChaosRewardsPageTests.cs" company="HNMO">
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
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// bUnit tests for Chaos Machine Rates, Multi-Vault, and Event Rewards Editor (<c>/vault-chaos-rewards</c>).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class VaultChaosRewardsPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _chaosPlugin = null!;
    private PlugInConfiguration _vaultPlugin = null!;
    private GameConfiguration _config = null!;
    private InMemoryAdminAuditRepository _audit = null!;
    private string _file = null!;

    /// <summary>Sets up bUnit context with ChaosRates, MultiVault, and a Blood Castle level reward.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-vcr-{Guid.NewGuid():N}.json");

        var provider = new InMemoryPersistenceContextProvider();
        using (var setup = provider.CreateNewContext())
        {
            this._config = setup.CreateNew<GameConfiguration>();
            var bc = setup.CreateNew<MiniGameDefinition>();
            bc.Type = MiniGameType.BloodCastle;
            bc.GameLevel = 1;
            bc.Name = "Blood Castle 1";
            var reward = setup.CreateNew<MiniGameReward>();
            reward.RewardType = MiniGameRewardType.Money;
            reward.RewardAmount = 50000;
            reward.RequiredSuccess = MiniGameSuccessFlags.Winner;
            bc.Rewards.Add(reward);
            this._config.MiniGameDefinitions.Add(bc);

            this._chaosPlugin = setup.CreateNew<PlugInConfiguration>();
            this._chaosPlugin.TypeId = typeof(ChaosRatesPlugIn).GUID;
            this._chaosPlugin.IsActive = true;

            this._vaultPlugin = setup.CreateNew<PlugInConfiguration>();
            this._vaultPlugin.TypeId = typeof(MultiVaultPlugIn).GUID;
            this._vaultPlugin.IsActive = true;
            this._vaultPlugin.CustomConfiguration = JsonSerializer.Serialize(new MultiVaultConfiguration { FileName = this._file });

            this._config.PlugInConfigurations.Add(this._chaosPlugin);
            this._config.PlugInConfigurations.Add(this._vaultPlugin);
            await setup.SaveChangesAsync().ConfigureAwait(false);
        }

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._config);
        var manager = new PlugInManager([], Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IFeaturePlugIn, ChaosRatesPlugIn>();
        manager.RegisterPlugIn<IFeaturePlugIn, MultiVaultPlugIn>();
        this._audit = new InMemoryAdminAuditRepository();
        var executor = AdminOperationTestServices.CreateExecutor(audit: this._audit);
        var writes = new PluginWriteOperations(provider, source.Object, manager, executor, new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });
        var rewardsOps = new EventRewardsOperations(provider, source.Object, executor);

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(rewardsOps);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<VaultChaosRewards>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Cleans up bUnit and temporary state.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }
    }

    /// <summary>Verifies Chaos rates save, Multi-vault save, and per-level Event Rewards add/save with audit entry.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SavesChaosRatesMultiVaultAndEventRewardsAsync()
    {
        var cut = this._context.Render<VaultChaosRewards>();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("#vcr-rewards-table tbody tr"), Has.Count.EqualTo(1)), LoadTimeout);

        cut.Find("#vcr-help-btn").Click();
        Assert.That(cut.Find("#vcr-help").TextContent, Contains.Substring("/vault"));

        cut.Find("#vcr-chaos-enabled").Change(true);
        cut.Find("#vcr-chaos-mult").Change("1.5");
        cut.Find("#vcr-chaos-add").Click();
        cut.Find("#vcr-chaos-save").Click();
        cut.WaitForAssertion(() =>
        {
            var cfg = JsonSerializer.Deserialize<ChaosRatesConfiguration>(this._chaosPlugin.CustomConfiguration ?? "{}");
            Assert.That(cfg?.Enabled, Is.True);
            Assert.That(cfg?.ServerWideMultiplier, Is.EqualTo(1.5).Within(0.01));
            Assert.That(cfg?.RecipeOverrides, Has.Count.EqualTo(1));
        }, LoadTimeout);

        cut.Find("#vcr-vault-enabled").Change(true);
        cut.Find("#vcr-v-def").Change("2");
        cut.Find("#vcr-vault-save").Click();
        cut.WaitForAssertion(() =>
        {
            var cfg = JsonSerializer.Deserialize<MultiVaultConfiguration>(this._vaultPlugin.CustomConfiguration ?? "{}");
            Assert.That(cfg?.Enabled, Is.True);
            Assert.That(cfg?.DefaultVaultCount, Is.EqualTo(2));
        }, LoadTimeout);

        cut.Find("#vcr-ev-add").Click();
        cut.Find("#vcr-ev-save").Click();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("#vcr-rewards-table tbody tr"), Has.Count.EqualTo(2)), LoadTimeout);

        var (audits, _) = await this._audit.ReadAsync(0, 50).ConfigureAwait(false);
        Assert.That(audits.Any(e => e.Operation == "event_rewards_save"), Is.True);
    }

    /// <summary>Verifies Vietnamese localization renders properly.</summary>
    [Test]
    public void RendersVietnameseLabels()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var cut = this._context.Render<VaultChaosRewards>();
        cut.WaitForAssertion(() => Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Tỉ lệ Chaos, Nhiều rương & Thưởng sự kiện")), LoadTimeout);
    }
}
