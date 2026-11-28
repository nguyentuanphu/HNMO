// <copyright file="WorldsPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Persistence.Worlds;
using HNMO.Web.AdminPanel.API;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.Tests.Mcp;

/// <summary>
/// bUnit and API tests for the Admin Panel Worlds page (<c>/worlds</c>) and <see cref="WebsiteController.GetWorlds"/> (ROADMAP 15.7).
/// </summary>
[TestFixture]
public sealed class WorldsPageTests
{
    private BunitContext _context = null!;
    private CultureInfo _previousCulture = null!;
    private WorldRegistryService _registry = null!;

    /// <summary>Sets up bUnit services and world registry.</summary>
    [SetUp]
    public void SetUp()
    {
        this._previousCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._registry = new WorldRegistryService();
        this._registry.EnsureDefaultWorld([0]);
        var provider = new InMemoryPersistenceContextProvider();
        this._registry.RegisterWorldContextProvider("main", provider);

        var serverProvider = new Mock<IServerProvider>();
        serverProvider.SetupGet(p => p.Servers).Returns(Array.Empty<IManageableServer>());

        var ops = new WorldsOperations(
            this._registry,
            serverProvider.Object,
            provider,
            AdminOperationTestServices.CreateExecutor());

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton(ops);
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Restores culture and disposes bUnit context.</summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousCulture;
    }

    /// <summary>Verifies English and Vietnamese labels, help toggle, and world table rendering.</summary>
    [Test]
    public void RendersWorldsPageInEnAndViWithHelpToggle()
    {
        var cut = this._context.Render<Worlds>();
        Assert.That(cut.Markup, Does.Contain("Worlds &amp; Multi-Realm Databases"));
        Assert.That(cut.Markup, Does.Contain("hnmo_world_1"));

        cut.Find("button[title='Show or hide usage help']").Click();
        Assert.That(cut.Markup, Does.Contain("How to use this page:"));

        cut.Find("button[title='Chuyển giao diện sang tiếng Việt']").Click();
        Assert.That(cut.Markup, Does.Contain("Quản lý Thế giới"));
        Assert.That(cut.Markup, Does.Contain("Hướng dẫn sử dụng trang này:"));
    }

    /// <summary>Verifies <see cref="WebsiteController.GetWorlds"/> exposes grouped world snapshots.</summary>
    [Test]
    public void WebsiteControllerExposesGroupedWorlds()
    {
        var serverMock = new Mock<IGameServer>();
        serverMock.SetupGet(s => s.Id).Returns(0);
        serverMock.SetupGet(s => s.CurrentConnections).Returns(7);
        serverMock.SetupGet(s => s.MaximumConnections).Returns(100);
        serverMock.SetupGet(s => s.ServerState).Returns(ServerState.Started);

        var controller = new WebsiteController(
            new Dictionary<int, IGameServer> { [0] = serverMock.Object },
            this._registry);

        var result = controller.GetWorlds() as OkObjectResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));
    }

    /// <summary>Each world shows its season, and the season can be chosen when a world is made.</summary>
    [Test]
    public void ShowsEachWorldsSeasonAndOffersTheChoice()
    {
        this._registry.UpsertWorld(new WorldDefinition { WorldId = "nine", NameEn = "World Nine", Season = WorldSeason.Season9, ServerIds = [5] });

        var cut = this._context.Render<Worlds>();

        var badges = cut.FindAll(".world-season-badge").Select(b => b.TextContent.Trim()).ToList();
        Assert.That(badges, Is.EquivalentTo(new[] { "S6.3", "S9" }));
        var options = cut.FindAll("#tplSeason option").Select(o => o.TextContent.Trim()).ToList();
        Assert.That(options, Is.EqualTo(new[] { "Season 6.3", "Season 9" }));
    }

    /// <summary>
    /// The host writes the text the game's server choice shows for a world, in English and Vietnamese, with a hint on
    /// each field; saving keeps it with the world.
    /// </summary>
    [Test]
    public void TheServerChoiceTextIsEditedAndSaved()
    {
        var cut = this._context.Render<Worlds>();
        cut.Find("button[title='Load this world into rate editor']").Click();

        Assert.That(cut.Find("label[title^='The text the game']").TextContent.Trim(), Is.EqualTo("Server choice text (EN)"));
        cut.Find("#editServerChoiceTextEn").Change("Fresh start, friendly rates");
        cut.Find("#editServerChoiceTextVi").Change("Khởi đầu mới, tỉ lệ thân thiện");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save Changes").Click();

        cut.WaitForAssertion(() =>
        {
            var main = this._registry.GetWorlds().Single(w => w.WorldId == "main");
            Assert.That((main.ServerChoiceTextEn, main.ServerChoiceTextVi), Is.EqualTo(("Fresh start, friendly rates", "Khởi đầu mới, tỉ lệ thân thiện")));
        });
    }

    /// <summary>
    /// Saving a world sets the rates of its own servers. The game's base rates and the servers of other worlds
    /// keep theirs (saving one world used to overwrite the base experience rate for every world).
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SavingAWorldChangesOnlyItsOwnServersRatesAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        using (var seed = provider.CreateNewContext())
        {
            var config = seed.CreateNew<HNMO.DataModel.Configuration.GameConfiguration>();
            config.ExperienceRate = 10f;
            config.MasterExperienceRate = 4f;
            foreach (var id in new byte[] { 0, 5 })
            {
                var definition = seed.CreateNew<HNMO.DataModel.Configuration.GameServerDefinition>();
                definition.ServerID = id;
                definition.ExperienceRate = 1f;
            }

            await seed.SaveChangesAsync().ConfigureAwait(false);
        }

        var serverProvider = new Mock<IServerProvider>();
        serverProvider.SetupGet(p => p.Servers).Returns(Array.Empty<IManageableServer>());
        var ops = new WorldsOperations(this._registry, serverProvider.Object, provider, AdminOperationTestServices.CreateExecutor());

        await ops.SaveWorldAsync(new WorldDefinition { WorldId = "nine", NameEn = "World Nine", Season = WorldSeason.Season9, ServerIds = [5], ExperienceMultiplier = 3.0 }).ConfigureAwait(false);

        using var check = provider.CreateNewContext();
        var savedConfig = (await check.GetAsync<HNMO.DataModel.Configuration.GameConfiguration>().ConfigureAwait(false)).Single();
        var servers = (await check.GetAsync<HNMO.DataModel.Configuration.GameServerDefinition>().ConfigureAwait(false)).ToDictionary(d => d.ServerID);
        Assert.That(savedConfig.ExperienceRate, Is.EqualTo(10f));
        Assert.That(savedConfig.MasterExperienceRate, Is.EqualTo(4f));
        Assert.That(servers[5].ExperienceRate, Is.EqualTo(3f));
        Assert.That(servers[0].ExperienceRate, Is.EqualTo(1f));
    }

    /// <summary>
    /// A character is not moved while its account is logged in: the login server refuses the reservation, and the
    /// world registry is never asked to move it.
    /// </summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ATransferIsRefusedWhileTheAccountIsLoggedInAsync()
    {
        var serverProvider = new Mock<IServerProvider>();
        serverProvider.SetupGet(p => p.Servers).Returns(Array.Empty<IManageableServer>());
        var loginServer = new Mock<ILoginServer>();
        loginServer.Setup(l => l.TryReserveAccountAsync("alice", It.IsAny<int>())).ReturnsAsync(false);
        var ops = new WorldsOperations(this._registry, serverProvider.Object, new InMemoryPersistenceContextProvider(), AdminOperationTestServices.CreateExecutor(), loginServer.Object);

        var refusal = Assert.ThrowsAsync<AdminOperationException>(async () => await ops.TransferCharacterOnceAsync("main", "s2", "alice", "Hero").ConfigureAwait(false));

        Assert.That(refusal!.Message, Does.Contain("logged in"));
        Assert.That(this._registry.GetTransfers(), Is.Empty);
        loginServer.Verify(l => l.ReleaseAccountReservationAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}
