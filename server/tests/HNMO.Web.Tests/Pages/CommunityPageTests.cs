// <copyright file="CommunityPageTests.cs" company="HNMO">
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
using HNMO.GameLogic.PlugIns.Community;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Shared.Components.Toast;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// Tests for the admin panel's "Community" page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class CommunityPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private Mock<IDataSource<GameConfiguration>> _source = null!;
    private string _fileName = null!;

    /// <summary>
    /// Sets up the page with a community configuration with one event and its own store file.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._fileName = Path.Combine(Path.GetTempPath(), $"hnmo-community-page-{Guid.NewGuid():N}.json");
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var config = setup.CreateNew<GameConfiguration>();
        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(CommunityPlugIn).GUID;
        this._plugin.IsActive = true;
        var configuration = new CommunityConfiguration { FileName = this._fileName };
        configuration.Events.Add(new CommunityEventDefinition { Id = "party", TitleEn = "Party", TitleVi = "Tiệc", Code = "CAKE" });
        this._plugin.CustomConfiguration = JsonSerializer.Serialize(configuration);
        config.PlugInConfigurations.Add(this._plugin);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        this._source = new Mock<IDataSource<GameConfiguration>>();
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, CommunityPlugIn>();
        var writes = new PluginWriteOperations(provider, this._source.Object, manager, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(provider);
        this._context.Services.AddSingleton(this._source.Object);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<Community>>());
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
        File.Delete(this._fileName);
    }

    /// <summary>
    /// The page lists the rewards, events and post kinds; edits, a new event with winners and items, and the drop filter
    /// are saved into the plug-in's configuration.
    /// </summary>
    [Test]
    public void EditsAreSavedIntoThePlugInConfiguration()
    {
        var cut = this._context.Render<Community>();
        cut.WaitForState(() => cut.FindAll(".community-event").Count == 1, LoadTimeout);
        Assert.That(cut.FindAll("#community-posts tbody tr"), Has.Count.EqualTo(Enum.GetValues<CommunityPostKind>().Length));
        Assert.That(cut.Markup, Does.Contain(Resources.CommunityKindCastleSiege));

        cut.Find("tr[data-reward='discord'] .reward-coins").Change("77");
        cut.Find("tr[data-reward='zalo'] .reward-coin-type").Change(nameof(CashShopCoinType.WCoinP));
        cut.Find("tr[data-kind='RareDrop'] .post-discord").Change(false);
        cut.Find("tr[data-kind='GuildLevelUp'] .post-text-vi").Change("Bang {guild} cấp {level}");
        cut.Find("#community-drops-level").Change("11");
        cut.Find("#community-drops-items").Change("14 13; 14,14");
        cut.Find("#community-drops-maps").Change("0, 7");
        cut.Find("#community-add-event").Click();
        var added = cut.FindAll(".community-event").Last();
        added.QuerySelector(".event-id")!.Change("screens");
        cut.FindAll(".community-event").Last().QuerySelector(".event-winners")!.Change("Ann, Bob");
        cut.FindAll(".community-event").Last().QuerySelector(".event-items")!.Change("14 13 0 2");
        cut.FindAll(".community-event").Last().QuerySelector(".event-start")!.Change("2026-12-01 18:00");
        cut.FindAll(".community-event").Last().QuerySelector(".event-end")!.Change("2026-12-08 18:00");
        cut.Find("#community-save").Click();

        cut.WaitForState(() => this._plugin.CustomConfiguration?.Contains("screens", StringComparison.Ordinal) is true, LoadTimeout);
        var saved = JsonSerializer.Deserialize<CommunityConfiguration>(this._plugin.CustomConfiguration!)!;
        Assert.That(saved.Discord.Coins, Is.EqualTo(77));
        Assert.That(saved.Zalo.CoinType, Is.EqualTo(CashShopCoinType.WCoinP));
        Assert.That(saved.RareDrop.ToDiscord, Is.False);
        Assert.That(saved.GuildLevelUp.TextVi, Is.EqualTo("Bang {guild} cấp {level}"));
        Assert.That(saved.Drops.MinimumLevel, Is.EqualTo(11));
        Assert.That(saved.Drops.Items, Is.EqualTo(new[] { "14 13", "14 14" }));
        Assert.That(saved.Drops.Maps, Is.EqualTo(new short[] { 0, 7 }));
        var screens = saved.Events.Single(e => e.Id == "screens");
        Assert.That(screens.Winners, Is.EqualTo(new[] { "Ann", "Bob" }));
        Assert.That(screens.Reward.Items.Single().Count, Is.EqualTo(2));
        Assert.That(screens.Start, Is.EqualTo("2026-12-01 18:00"));
        Assert.That(saved.Events.Single(e => e.Id == "party").Code, Is.EqualTo("CAKE"));
    }

    /// <summary>
    /// Invalid entries are listed and nothing is saved.
    /// </summary>
    [Test]
    public void InvalidEntriesAreNotSaved()
    {
        var before = this._plugin.CustomConfiguration;
        var cut = this._context.Render<Community>();
        cut.WaitForState(() => cut.FindAll(".community-event").Count == 1, LoadTimeout);
        cut.Find("#community-add-event").Click();
        var added = cut.FindAll(".community-event").Last();
        added.QuerySelector(".event-code")!.Change("cake");
        cut.FindAll(".community-event").Last().QuerySelector(".event-end")!.Change("yesterday");
        cut.Find("#community-drops-maps").Change("Lorencia");
        cut.Find("#community-save").Click();
        cut.WaitForState(() => cut.FindAll("#community-errors li").Count > 0, LoadTimeout);
        var errors = cut.Find("#community-errors").TextContent;
        Assert.That(errors, Does.Contain(string.Format(Resources.CommunityErrorDuplicateCode, "event2")));
        Assert.That(errors, Does.Contain(string.Format(Resources.CommunityErrorDates, "event2")));
        Assert.That(errors, Does.Contain(string.Format(Resources.CommunityErrorMap, "Lorencia")));
        Assert.That(this._plugin.CustomConfiguration, Is.EqualTo(before));
    }

    /// <summary>
    /// A waiting Zalo claim is listed and approved into the shared store (the game server then pays it).
    /// </summary>
    [Test]
    public void ZaloClaimsAreApproved()
    {
        var engine = new CommunityEngine(new CommunityConfiguration(), CommunityStore.Open(this._fileName));
        var (_, code) = engine.RequestZalo("test7", "Ann", "Ann Zalo", DateTime.UtcNow);
        var cut = this._context.Render<Community>();
        cut.WaitForState(() => cut.FindAll("#community-zalo tbody tr").Count == 1, LoadTimeout);
        Assert.That(cut.Find("#community-zalo").TextContent, Does.Contain(code!).And.Contain("Ann Zalo"));
        cut.Find("tr[data-login='test7'] .zalo-approve").Click();
        cut.WaitForState(() => cut.FindAll("#community-zalo-empty").Count == 1, LoadTimeout);
        Assert.That(engine.ZaloToPay().Single().LoginName, Is.EqualTo("test7"));
        Assert.That(cut.Find("#community-zalo-decided").TextContent, Does.Contain(Resources.CommunityApprovedWaiting));
    }

    /// <summary>
    /// The page reads the plug-in's row from the database, not from the panel's cached configuration, which can miss a
    /// row the game server added at its first start; and it names the page in Vietnamese.
    /// </summary>
    [Test]
    public void TheSettingsAreReadFromTheDatabaseInBothLanguages()
    {
        this._source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HNMO.Persistence.BasicModel.GameConfiguration());
        var cut = this._context.Render<Community>();
        cut.WaitForState(() => cut.FindAll(".community-event").Count == 1, LoadTimeout);
        Assert.That(cut.Markup, Does.Not.Contain(Resources.CommunityNotInstalled));
        Assert.That(cut.Find("#community-highlights-empty").TextContent, Is.EqualTo(Resources.CommunityNoHighlights));

        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi");
        var vietnamese = this._context.Render<Community>();
        vietnamese.WaitForState(() => vietnamese.FindAll(".community-event").Count == 1, LoadTimeout);
        Assert.That(vietnamese.Find("h1").TextContent, Is.EqualTo("Cộng đồng"));
        Assert.That(vietnamese.Markup, Does.Contain("Duyệt").Or.Contain("Sự kiện cộng đồng"));
    }
}
