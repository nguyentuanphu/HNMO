// <copyright file="WorldBossesRankingsPageTests.cs" company="HNMO">
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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Tests for the admin panel's "World bosses and rankings" page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class WorldBossesRankingsPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private Mock<IToastService> _toasts = null!;
    private string _file = null!;

    /// <summary>
    /// Sets up the page with the default world bosses configuration and a kill in its store.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-bosses-page-{Guid.NewGuid():N}.json");
        var configuration = new WorldBossesConfiguration { FileName = this._file };

        // A kill with a damage table.
        var engine = new WorldBossEngine(configuration, WorldBossStore.Open(this._file));
        var boss = configuration.Bosses.First();
        var alive = engine.RegisterSpawn(boss, 0, 10, 20, 100_000, DateTime.UtcNow.AddMinutes(-5));
        engine.RecordDamage(alive.InstanceId, "c1", "Hero", "test1", null, 6000);
        engine.RecordDamage(alive.InstanceId, "c2", "Helper", "test2", null, 4000);
        engine.Kill(alive.InstanceId, "c2", DateTime.UtcNow);

        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var config = setup.CreateNew<GameConfiguration>();
        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(WorldBossesPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(configuration);
        config.PlugInConfigurations.Add(this._plugin);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, WorldBossesPlugIn>();
        var writes = new PluginWriteOperations(provider, source.Object, manager, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });

        this._toasts = new Mock<IToastService>();
        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton(source.Object);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(this._toasts.Object);
        this._context.Services.AddSingleton<IList<IManageableServer>>(new List<IManageableServer>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<WorldBossesRankings>>());
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
        File.Delete(this._file);
    }

    /// <summary>
    /// The page lists the bosses; edits of a schedule, a reward, a ranking and a new boss are saved into the plug-in's configuration.
    /// </summary>
    [Test]
    public void EditsAreSavedIntoThePlugInConfiguration()
    {
        var cut = this._context.Render<WorldBossesRankings>();
        cut.WaitForState(() => cut.FindAll(".wb-boss").Count == 3, LoadTimeout);
        Assert.That(cut.Markup, Does.Contain("Red Dragon").And.Contain("Rồng Đỏ"));
        Assert.That(cut.Find("#rank-bloodcastle-ranks").GetAttribute("value"), Is.EqualTo("1:200, 2:120, 3:80, 4:30, 5:30, 6:30, 7:30, 8:30, 9:30, 10:30"));

        cut.Find("#wb-1-times").Change("8:00; 21:30");
        cut.Find("#wb-1-day-6").Change(true);
        cut.Find("#wb-1-points").Change("25");
        cut.Find("#rank-bosshunt-ranks").Change("1:500, 2:250");
        cut.Find("#rank-castlesiege-coins").Change("150");
        cut.Find("#wb-add").Click();
        cut.Find("#wb-save").Click();

        cut.WaitForState(() => this._plugin.CustomConfiguration?.Contains("boss-4", StringComparison.Ordinal) is true, LoadTimeout);
        var saved = JsonSerializer.Deserialize<WorldBossesConfiguration>(this._plugin.CustomConfiguration!)!;
        var dragon = saved.Bosses.Single(b => b.Id == "red-dragon");
        Assert.That(dragon.FixedTimes, Is.EqualTo("08:00, 21:30"));
        Assert.That(dragon.Days, Is.EqualTo(new[] { DayOfWeek.Saturday }));
        Assert.That(dragon.HuntPoints, Is.EqualTo(25));
        Assert.That(dragon.Top1.Items.Single().Number, Is.EqualTo(14), "The reward items survive the round trip.");
        Assert.That(saved.Bosses, Has.Count.EqualTo(4));
        Assert.That(saved.BossHunt.CoinsOf(1), Is.EqualTo(500));
        Assert.That(saved.BossHunt.CoinsOf(3), Is.Zero, "Rank 3 was taken out.");
        Assert.That(saved.CastleSiege.CoinsPerMember, Is.EqualTo(150));
    }

    /// <summary>
    /// Invalid entries are listed and nothing is saved.
    /// </summary>
    [Test]
    public void InvalidEntriesAreNotSaved()
    {
        var before = this._plugin.CustomConfiguration;
        var cut = this._context.Render<WorldBossesRankings>();
        cut.WaitForState(() => cut.FindAll(".wb-boss").Count == 3, LoadTimeout);
        cut.Find("#wb-1-times").Change("25:99");
        cut.Find("#rank-devilsquare-ranks").Change("11:5");
        cut.Find("#wb-save").Click();
        cut.WaitForState(() => cut.FindAll("#wb-errors li").Count >= 2, LoadTimeout);
        Assert.That(cut.Find("#wb-errors").TextContent, Does.Contain(string.Format(CultureInfo.CurrentCulture, Resources.WorldBossesErrorTimes, "red-dragon")));
        Assert.That(this._plugin.CustomConfiguration, Is.EqualTo(before));
    }

    /// <summary>
    /// The recent kills show the damage table; "spawn now" without a running game server says so.
    /// </summary>
    [Test]
    public void KillsAreListedAndSpawnNowNeedsAServer()
    {
        var cut = this._context.Render<WorldBossesRankings>();
        cut.WaitForState(() => cut.FindAll(".wb-kill").Count == 1, LoadTimeout);
        var kill = cut.Find(".wb-kill").TextContent;
        Assert.That(kill, Does.Contain("Ice Queen").And.Contain("Hero").And.Contain("6,000").And.Contain("60%").And.Contain("Last hit: Helper"));

        cut.Find(".wb-boss[data-boss='ice-queen'] .wb-spawn").Click();
        cut.WaitForAssertion(() => this._toasts.Verify(t => t.ShowError(It.Is<string>(m => m.Contains("No running game server", StringComparison.Ordinal)), It.IsAny<string?>()), Times.Once), LoadTimeout);
    }
}
