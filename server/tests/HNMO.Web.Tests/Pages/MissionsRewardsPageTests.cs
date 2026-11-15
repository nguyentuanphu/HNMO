// <copyright file="MissionsRewardsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Text.Json;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Tests for the admin panel's "Missions and rewards" page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class MissionsRewardsPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;

    /// <summary>
    /// Sets up the page with the default missions configuration.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var config = setup.CreateNew<GameConfiguration>();
        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(MissionsPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(new MissionsConfiguration());
        config.PlugInConfigurations.Add(this._plugin);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, MissionsPlugIn>();
        var writes = new PluginWriteOperations(provider, source.Object, manager, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton(source.Object);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<MissionsRewards>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>
    /// Restores the culture and disposes the test context.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
    }

    /// <summary>
    /// The page lists the missions; an edit and a new mission are saved into the plug-in's configuration.
    /// </summary>
    [Test]
    public void EditsAreSavedIntoThePlugInConfiguration()
    {
        var cut = this._context.Render<MissionsRewards>();
        cut.WaitForState(() => cut.FindAll("#missions-table tbody tr").Count == 9, LoadTimeout);
        Assert.That(cut.Markup, Does.Contain("Defeat 300 monsters").And.Contain("Hạ gục 300 quái vật"));
        Assert.That(cut.Find("#milestone-levels").GetAttribute("value"), Is.EqualTo("100:20, 200:30, 300:50, 400:80"));

        cut.Find("#missions-cap").Change("250");
        cut.Find("#calendar-days").Change("7:25, 28:70");
        cut.Find("#missions-add").Click();
        cut.Find("#missions-save").Click();

        cut.WaitForState(() => this._plugin.CustomConfiguration?.Contains("mission-10", StringComparison.Ordinal) is true, LoadTimeout);
        var saved = JsonSerializer.Deserialize<MissionsConfiguration>(this._plugin.CustomConfiguration!)!;
        Assert.That(saved.EarnedDailyCap, Is.EqualTo(250));
        Assert.That(saved.Missions, Has.Count.EqualTo(10));
        Assert.That(saved.LoginCalendar.CoinsOf(28), Is.EqualTo(70));
        Assert.That(saved.LoginCalendar.CoinsOf(14), Is.EqualTo(saved.LoginCalendar.DefaultCoins), "Day 14 was taken out.");
    }

    /// <summary>
    /// Invalid entries are listed and nothing is saved.
    /// </summary>
    [Test]
    public void InvalidEntriesAreNotSaved()
    {
        var before = this._plugin.CustomConfiguration;
        var cut = this._context.Render<MissionsRewards>();
        cut.WaitForState(() => cut.FindAll("#missions-table tbody tr").Count == 9, LoadTimeout);
        cut.Find("#calendar-days").Change("40:5");
        cut.Find("#missions-save").Click();
        cut.WaitForState(() => cut.FindAll("#missions-errors li").Count > 0, LoadTimeout);
        Assert.That(cut.Find("#missions-errors").TextContent, Does.Contain(Resources.MissionsErrorDays));
        Assert.That(this._plugin.CustomConfiguration, Is.EqualTo(before));
    }
}
