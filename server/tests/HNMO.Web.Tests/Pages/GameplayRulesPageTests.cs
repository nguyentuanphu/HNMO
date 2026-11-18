// <copyright file="GameplayRulesPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Text.Json;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// bUnit tests for the admin panel Gameplay Rules page (<c>/gameplay-rules</c>).
/// </summary>
[TestFixture]
public sealed class GameplayRulesPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _mapPlugin = null!;
    private PlugInConfiguration _bonusPlugin = null!;
    private PlugInConfiguration _combatPlugin = null!;

    /// <summary>Sets up bUnit context and plugin configurations.</summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        var provider = new InMemoryPersistenceContextProvider();
        GameConfiguration config;
        using (var setup = provider.CreateNewContext())
        {
            config = setup.CreateNew<GameConfiguration>();
            this._mapPlugin = setup.CreateNew<PlugInConfiguration>();
            this._mapPlugin.TypeId = typeof(MapRulesPlugIn).GUID;
            this._mapPlugin.IsActive = true;

            this._bonusPlugin = setup.CreateNew<PlugInConfiguration>();
            this._bonusPlugin.TypeId = typeof(BonusEventsPlugIn).GUID;
            this._bonusPlugin.IsActive = true;

            this._combatPlugin = setup.CreateNew<PlugInConfiguration>();
            this._combatPlugin.TypeId = typeof(CombatLimitsPlugIn).GUID;
            this._combatPlugin.IsActive = true;

            config.PlugInConfigurations.Add(this._mapPlugin);
            config.PlugInConfigurations.Add(this._bonusPlugin);
            config.PlugInConfigurations.Add(this._combatPlugin);
            await setup.SaveChangesAsync().ConfigureAwait(false);
        }

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var manager = new PlugInManager([], Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerAttackRestrictionPlugIn, MapRulesPlugIn>();
        manager.RegisterPlugIn<IPeriodicTaskPlugIn, BonusEventsPlugIn>();
        manager.RegisterPlugIn<IFeaturePlugIn, CombatLimitsPlugIn>();
        var writes = new PluginWriteOperations(
            provider,
            source.Object,
            manager,
            AdminOperationTestServices.CreateExecutor(),
            new AdminChangeConfirmation(TimeProvider.System),
            new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<GameplayRules>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Restores culture and disposes bUnit.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
    }

    /// <summary>Verifies toggling help, adding map/bonus rules, configuring combat limits, and saving all three plug-ins.</summary>
    [Test]
    public void ConfiguresAndSavesMapBonusAndCombatRules()
    {
        var cut = this._context.Render<GameplayRules>();
        cut.WaitForAssertion(() => Assert.That(cut.Find("#gr-map-card"), Is.Not.Null), LoadTimeout);

        cut.Find("#gr-help-btn").Click();
        Assert.That(cut.Find("#gameplay-rules-help").TextContent, Contains.Substring("Sydney local time"));

        cut.Find("#gr-map-enabled").Change(true);
        cut.Find("#gr-map-add").Click();
        cut.Find("#gr-map-save").Click();
        cut.WaitForAssertion(() =>
        {
            var saved = JsonSerializer.Deserialize<MapRulesConfiguration>(this._mapPlugin.CustomConfiguration ?? "{}");
            Assert.That(saved?.Enabled, Is.True);
            Assert.That(saved?.Rules, Has.Count.EqualTo(1));
        }, LoadTimeout);

        cut.Find("#gr-bonus-enabled").Change(true);
        cut.Find("#gr-bonus-add").Click();
        cut.Find("#gr-bonus-save").Click();
        cut.WaitForAssertion(() =>
        {
            var saved = JsonSerializer.Deserialize<BonusEventsConfiguration>(this._bonusPlugin.CustomConfiguration ?? "{}");
            Assert.That(saved?.Enabled, Is.True);
            Assert.That(saved?.Events, Has.Count.EqualTo(1));
        }, LoadTimeout);

        cut.Find("#gr-combat-enabled").Change(true);
        cut.Find("#gr-max-dmg").Change("15000");
        cut.Find("#gr-pvp-red").Change("35");
        cut.Find("#gr-combat-save").Click();
        cut.WaitForAssertion(() =>
        {
            var saved = JsonSerializer.Deserialize<CombatLimitsConfiguration>(this._combatPlugin.CustomConfiguration ?? "{}");
            Assert.That(saved?.Enabled, Is.True);
            Assert.That(saved?.MaxDamagePerHit, Is.EqualTo(15000));
            Assert.That(saved?.PvpDamageReductionPercent, Is.EqualTo(35));
        }, LoadTimeout);
    }

    /// <summary>Verifies Vietnamese localization renders properly.</summary>
    [Test]
    public void RendersVietnameseLabels()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var cut = this._context.Render<GameplayRules>();
        cut.WaitForAssertion(() => Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Luật bản đồ, Giờ vàng & Giới hạn chiến đấu")), LoadTimeout);
    }
}
