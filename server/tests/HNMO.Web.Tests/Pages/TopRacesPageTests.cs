// <copyright file="TopRacesPageTests.cs" company="HNMO">
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
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Tests for the admin panel's "Top races" page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class TopRacesPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private Mock<IDataSource<GameConfiguration>> _source = null!;
    private GameConfiguration _config = null!;

    /// <summary>
    /// Sets up the page with the default top races configuration.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        this._config = setup.CreateNew<GameConfiguration>();
        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(TopRacesPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(new TopRacesConfiguration { FileName = string.Empty });
        this._config.PlugInConfigurations.Add(this._plugin);
        var knight = setup.CreateNew<CharacterClass>();
        knight.Number = 4;
        knight.Name = "Dark Knight";
        knight.CanGetCreated = true;
        this._config.CharacterClasses.Add(knight);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        this._source = new Mock<IDataSource<GameConfiguration>>();
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._config);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, TopRacesPlugIn>();
        var writes = new PluginWriteOperations(provider, this._source.Object, manager, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(this._source.Object);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<TopRaces>>());
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
    /// The page lists the default races with every field; edits, a new custom race with a prize and a removed race are
    /// saved into the plug-in's configuration.
    /// </summary>
    [Test]
    public void EditsAreSavedIntoThePlugInConfiguration()
    {
        var cut = this._context.Render<TopRaces>();
        cut.WaitForState(() => cut.FindAll(".top-race").Count == 6, LoadTimeout);
        Assert.That(cut.Find(".top-race[data-race='monthly-levels'] .race-scope").GetAttribute("value"), Is.EqualTo(nameof(TopRaceScope.PerClass)));
        Assert.That(cut.Find(".top-race[data-race='weekly-events'] .race-event"), Is.Not.Null, "Event wins choose the event.");
        Assert.That(cut.Markup, Does.Contain(Resources.TopRacesKindBossHuntPoints));
        Assert.That(cut.FindAll(".top-race-standings[data-race='weekly-resets']"), Has.Count.EqualTo(1), "The live standings show the running races.");

        cut.Find("#top-races-history").Change("12");
        cut.Find(".top-race[data-race='weekly-resets'] .race-minimum").Change("3");
        cut.Find(".top-race[data-race='weekly-resets'] tr[data-rank='1'] .prize-coins").Change("444");
        cut.Find(".top-race[data-race='daily-monsters'] .race-remove").Click();
        cut.Find("#top-races-add").Click();
        var added = cut.FindAll(".top-race").Last();
        added.QuerySelector(".race-id")!.Change("xmas");
        cut.FindAll(".top-race").Last().QuerySelector(".race-kind")!.Change(nameof(TopRaceKind.LevelReached));
        cut.FindAll(".top-race").Last().QuerySelector(".race-scope")!.Change(nameof(TopRaceScope.OneClass));
        cut.FindAll(".top-race").Last().QuerySelector(".race-class")!.Change(nameof(TopRaceClass.Summoner));
        cut.FindAll(".top-race").Last().QuerySelector(".race-period")!.Change(nameof(TopRacePeriod.Custom));
        cut.FindAll(".top-race").Last().QuerySelector(".race-start")!.Change("2026-12-20 20:00");
        cut.FindAll(".top-race").Last().QuerySelector(".race-end")!.Change("2026-12-27T20:00");
        cut.FindAll(".top-race").Last().QuerySelector(".prize-add")!.Click();
        cut.FindAll(".top-race").Last().QuerySelectorAll(".prize-items").Last().Change("14 13 0 2");
        cut.Find("#top-races-save").Click();

        cut.WaitForState(() => this._plugin.CustomConfiguration?.Contains("xmas", StringComparison.Ordinal) is true, LoadTimeout);
        var saved = JsonSerializer.Deserialize<TopRacesConfiguration>(this._plugin.CustomConfiguration!)!;
        Assert.That(saved.HistoryKept, Is.EqualTo(12));
        Assert.That(saved.Races.Select(r => r.Id), Does.Not.Contain("daily-monsters"));
        var resets = saved.Races.Single(r => r.Id == "weekly-resets");
        Assert.That(resets.Minimum, Is.EqualTo(3));
        Assert.That(resets.Prizes.First().Coins, Is.EqualTo(444));
        var xmas = saved.Races.Single(r => r.Id == "xmas");
        Assert.That((xmas.Kind, xmas.Scope, xmas.Class, xmas.Period), Is.EqualTo((TopRaceKind.LevelReached, TopRaceScope.OneClass, TopRaceClass.Summoner, TopRacePeriod.Custom)));
        Assert.That((xmas.Start, xmas.End), Is.EqualTo(("2026-12-20 20:00", "2026-12-27 20:00")), "The dates are normalized.");
        Assert.That(xmas.Prizes, Has.Count.EqualTo(2));
        Assert.That(xmas.Prizes.Last().Items.Single().Count, Is.EqualTo(2));
    }

    /// <summary>
    /// Invalid entries are listed and nothing is saved.
    /// </summary>
    [Test]
    public void InvalidEntriesAreNotSaved()
    {
        var before = this._plugin.CustomConfiguration;
        var cut = this._context.Render<TopRaces>();
        cut.WaitForState(() => cut.FindAll(".top-race").Count > 0, LoadTimeout);
        cut.Find(".top-race[data-race='weekly-pk'] .race-id").Change("weekly-resets");
        cut.Find(".top-race[data-race='weekly-boss-hunt'] tr[data-rank='4'] .prize-from").Change("3");
        cut.Find(".top-race[data-race='weekly-events'] .race-period").Change(nameof(TopRacePeriod.Custom));
        cut.Find("#top-races-save").Click();
        cut.WaitForState(() => cut.FindAll("#top-races-errors li").Count > 0, LoadTimeout);
        var errors = cut.Find("#top-races-errors").TextContent;
        Assert.That(errors, Does.Contain(string.Format(Resources.TopRacesErrorDuplicateId, "weekly-resets")));
        Assert.That(errors, Does.Contain(string.Format(Resources.TopRacesErrorRanks, "weekly-boss-hunt", 3, 10)));
        Assert.That(errors, Does.Contain(string.Format(Resources.TopRacesErrorDates, "weekly-events")));
        Assert.That(this._plugin.CustomConfiguration, Is.EqualTo(before));
    }

    /// <summary>
    /// The page reads the plug-in's row from the database, not from the panel's cached configuration, which can miss a
    /// row the game server added at its first start; and it names the page in Vietnamese.
    /// </summary>
    [Test]
    public void TheSettingsAreReadFromTheDatabaseInBothLanguages()
    {
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HNMO.Persistence.BasicModel.GameConfiguration());
        var cut = this._context.Render<TopRaces>();
        cut.WaitForState(() => cut.FindAll(".top-race").Count > 0, LoadTimeout);
        Assert.That(cut.Markup, Does.Not.Contain(Resources.TopRacesNotInstalled));
        Assert.That(cut.Find("#top-races-no-history").TextContent, Is.EqualTo(Resources.TopRacesNoHistory));

        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var vietnamese = this._context.Render<TopRaces>();
        vietnamese.WaitForState(() => vietnamese.FindAll(".top-race").Count > 0, LoadTimeout);
        Assert.That(vietnamese.Find("h1").TextContent, Is.EqualTo("Đua top"));
        Assert.That(vietnamese.Markup, Does.Contain("Đua top reset tuần"));
    }
}
