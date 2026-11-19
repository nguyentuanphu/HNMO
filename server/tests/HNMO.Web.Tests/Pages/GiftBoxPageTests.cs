// <copyright file="GiftBoxPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Threading;
using Bunit;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Tests.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;

/// <summary>Renders the real bilingual Gift Box admin page with ItemPicker, review step, and queued mail persistence.</summary>
[TestFixture]
[NonParallelizable]
public class GiftBoxPageTests
{
    private BunitContext _context = null!;
    private CultureInfo _previousCulture = null!;
    private CultureInfo? _previousResource = null!;
    private InMemoryHnmoPlayerStateRowBackend _rows = null!;
    private InMemoryAdminAuditRepository _audit = null!;
    private Account _account = null!;
    private readonly List<ActivityLogEntry> _activities = [];

    /// <summary>Sets up the isolated persistence, configuration, item catalog, and GiftBoxAdminOperations.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousCulture = CultureInfo.CurrentUICulture;
        this._previousResource = Resources.Culture;
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var game = setup.CreateNew<GameConfiguration>();
        var sword = setup.CreateNew<ItemDefinition>();
        sword.Group = 0;
        sword.Number = 0;
        sword.Width = 1;
        sword.Height = 1;
        sword.MaximumItemLevel = 15;
        sword.IsActive = true;
        sword.Name = new LocalizedString("Sword").WithTranslation(CultureInfo.GetCultureInfo("vi"), "Kiếm");
        game.Items.Add(sword);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        using var players = provider.CreateNewPlayerContext(game);
        this._account = players.CreateNew<Account>();
        this._account.LoginName = "ann";
        await players.SaveChangesAsync().ConfigureAwait(false);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(value => value.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var login = new Mock<ILoginServer>();
        this._rows = new InMemoryHnmoPlayerStateRowBackend();
        this._audit = new();
        this._activities.Clear();
        var executor = AdminOperationTestServices.CreateExecutor(audit: this._audit);
        var confirmations = new AdminChangeConfirmation(TimeProvider.System);
        var settings = new AdminWriteSettings { ReadOnly = false };
        var ops = new GiftBoxAdminOperations(provider, source.Object, login.Object, executor, confirmations, settings, TimeProvider.System, this._rows, this._activities.Add);

        this._context = new();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddLogging();
        this._context.Services.AddSingleton(source.Object);
        this._context.Services.AddSingleton(ops);
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Restores culture and disposes rendered components.</summary>
    /// <returns>The cleanup task.</returns>
    [TearDown]
    public async Task TearDownAsync()
    {
        await this._context.DisposeAsync().ConfigureAwait(false);
        CultureInfo.CurrentUICulture = this._previousCulture;
        Resources.Culture = this._previousResource;
        HNMO.Web.Shared.Properties.Resources.Culture = this._previousResource;
    }

    /// <summary>EN and VI render PageHelp, hover hints, ItemPicker gift mode, preview snapshot, and confirmed delivery.</summary>
    /// <param name="language">The admin UI language.</param>
    [TestCase("en")]
    [TestCase("vi")]
    public void RendersBilingualHelpHintsReviewAndSend(string language)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        Resources.Culture = CultureInfo.CurrentUICulture;
        HNMO.Web.Shared.Properties.Resources.Culture = CultureInfo.CurrentUICulture;
        var page = this._context.Render<GiftBox>();
        page.WaitForAssertion(() => Assert.That(page.Find("h1").TextContent, Is.EqualTo(language == "vi" ? "Hộp thư quà tặng" : "Gift box / Mail")));

        page.Find(".page-help button").Click();
        Assert.That(page.Find(".alert-info").TextContent, Does.Contain("Alt+M"));
        Assert.That(page.Find("label[for='gb-has-expiry']").TextContent, Does.Contain("Sydney"));

        page.Find(".picker-add").Click();
        page.Find("#gb-logins").Change("ann");
        page.Find("#gb-title-en").Change("Welcome");
        page.Find("#gb-title-vi").Change("Chào mừng");
        page.Find("#gb-msg-en").Change("Check bag");
        page.Find("#gb-msg-vi").Change("Kiểm tra túi");
        page.Find("#gb-coins").Change("25");
        page.Find("#gb-reason").Change("Welcome reward");
        page.Find("#gift-box-review-btn").Click();

        page.WaitForAssertion(() => Assert.That(page.FindAll("#gift-box-confirm-btn"), Has.Count.EqualTo(1)));
        Assert.That(new GiftBoxDispatch(this._rows).Read(this._account.GetId()), Is.Empty);

        page.Find("#gift-box-confirm-btn").Click();
        page.WaitForAssertion(() => Assert.That(page.Find("#gift-box-status").TextContent, Does.Contain("1")));

        var letters = new GiftBoxDispatch(this._rows).Read(this._account.GetId());
        Assert.That(letters, Has.Count.EqualTo(1));
        Assert.That(letters.Single().Reward.Coins, Is.EqualTo(25));
        Assert.That(letters.Single().Reward.Items, Has.Count.EqualTo(1));
        Assert.That(this._activities, Has.Count.EqualTo(2));
    }

    /// <summary>The route requires administrator access.</summary>
    [Test]
    public void RequiresAdministrator()
        => Assert.That(typeof(GiftBox).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Select(attribute => attribute.Policy), Does.Contain(AdminPolicies.Administrator));
}
