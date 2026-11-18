// <copyright file="VipManagerPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Threading;
using Bunit;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.AdminPanel.Pages;
using HNMO.Web.AdminPanel.Properties;
using HNMO.Web.Tests.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Renders the real bilingual page with authorized operations, configuration writes and an isolated account.</summary>
[TestFixture]
[NonParallelizable]
public class VipManagerPageTests
{
    private BunitContext _context = null!;
    private CultureInfo _previousCulture = null!;
    private CultureInfo? _previousResource = null!;
    private PlugInConfiguration _plugin = null!;
    private Account _account = null!;
    private InMemoryAdminAuditRepository _audit = null!;
    private readonly List<ActivityLogEntry> _activities = [];

    /// <summary>Creates the page's real service graph without a server, disk state or network SMTP dependency.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousCulture = CultureInfo.CurrentUICulture;
        this._previousResource = Resources.Culture;
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var game = setup.CreateNew<GameConfiguration>();
        foreach (var definition in new[] { Stats.IsVip, Stats.BonusExperienceRate, Stats.MoneyAmountRate })
        {
            game.Attributes.Add(setup.CreateNew<AttributeDefinition>(definition.Id, definition.Designation, definition.Description));
        }

        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(VipPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.SetConfiguration(new VipConfiguration(), null);
        game.PlugInConfigurations.Add(this._plugin);
        await setup.SaveChangesAsync().ConfigureAwait(false);
        using var players = provider.CreateNewPlayerContext(game);
        this._account = players.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.WCoinC = 1000;
        await players.SaveChangesAsync().ConfigureAwait(false);
        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(value => value.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var login = new Mock<ILoginServer>();
        login.Setup(value => value.TryReserveAccountAsync("ann", It.IsAny<int>())).ReturnsAsync(true);
        login.Setup(value => value.ReleaseAccountReservationAsync("ann", It.IsAny<int>())).Returns(ValueTask.CompletedTask);
        this._audit = new();
        var executor = AdminOperationTestServices.CreateExecutor(audit: this._audit);
        var confirmations = new AdminChangeConfirmation(TimeProvider.System);
        var settings = new AdminWriteSettings { ReadOnly = false };
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, VipPlugIn>();
        var plugins = new PluginWriteOperations(provider, source.Object, manager, executor, confirmations, settings);
        this._activities.Clear();
        var operations = new VipAdminOperations(provider, source.Object, login.Object, executor, confirmations, settings, NullLogger<VipAdminOperations>.Instance, TimeProvider.System, VipStateStore.CreateInMemory(), entry => this._activities.Add(entry));
        this._context = new();
        this._context.Services.AddLogging();
        this._context.Services.AddSingleton(plugins);
        this._context.Services.AddSingleton(operations);
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Restores process resource culture and releases rendered components.</summary>
    /// <returns>The cleanup task.</returns>
    [TearDown]
    public async Task TearDownAsync()
    {
        await this._context.DisposeAsync().ConfigureAwait(false);
        CultureInfo.CurrentUICulture = this._previousCulture;
        Resources.Culture = this._previousResource;
    }

    /// <summary>Both languages have help/hints, save real settings, and grant/remove only after a reviewed confirmation.</summary>
    /// <param name="language">The admin interface language.</param>
    [TestCase("en")]
    [TestCase("vi")]
    public void SavesSettingsAndReviewsActualGrantAndRemoval(string language)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        Resources.Culture = CultureInfo.CurrentUICulture;
        var page = this._context.Render<VipManager>();
        page.WaitForAssertion(() => Assert.That(page.Find("h1").TextContent, Is.EqualTo(language == "vi" ? "Quản lý VIP" : "VIP memberships")));
        page.Find(".page-help button").Click();
        Assert.That(page.Find(".alert-info").TextContent, Does.Contain("Sydney"));
        Assert.That(page.FindAll("input,button,textarea,select").All(control => !string.IsNullOrWhiteSpace(control.GetAttribute("title"))), Is.True);
        page.Find("#vip-enabled").Change(true);
        page.FindAll("input").First(input => input.GetAttribute("title") == Resources.VipManagerPriceHint).Change("150");
        page.FindAll("input[maxlength='60']")[1].Change("Đồng mới");
        page.Find("#vip-save").Click();
        page.WaitForAssertion(() => Assert.That(this._plugin.GetConfiguration<VipConfiguration>(null)!.Enabled, Is.True));
        page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain(Resources.VipManagerSaved)));
        var saved = this._plugin.GetConfiguration<VipConfiguration>(null)!;
        Assert.That(saved.Levels[0].Offers[0].Price, Is.EqualTo(150));
        Assert.That(saved.Levels[0].NameVi, Is.EqualTo("Đồng mới"));
        page.Find("#vip-account").Change("ann");
        page.Find("#vip-reason").Change("Maintenance compensation");
        page.Find("#vip-review-grant").Click();
        page.WaitForAssertion(() => Assert.That(page.FindAll("#vip-confirm"), Has.Count.EqualTo(1)));
        Assert.That(this._account.AttributeGrants, Is.Empty);
        Assert.That(this._activities, Is.Empty);
        page.Find("#vip-confirm").Click();
        page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain(Resources.VipManagerAppliedGrant)));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(this._activities, Has.Count.EqualTo(2));
        Assert.That(page.Find("table").TextContent, Does.Contain("ann"));
        page.Find("#vip-review-remove").Click();
        page.WaitForAssertion(() => Assert.That(page.FindAll("#vip-confirm"), Has.Count.EqualTo(1)));
        page.Find("#vip-confirm").Click();
        page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain(Resources.VipManagerAppliedRemove)));
        Assert.That(this._activities, Has.Count.EqualTo(4));
        Assert.That(this._account.AttributeGrants.All(grant => grant.ValidUntil <= DateTime.UtcNow), Is.True);
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
    }

    /// <summary>A lost audit completion keeps a retryable operation and does not extend the interval twice.</summary>
    [Test]
    public void UncertainOutcomeRetriesSameGrant()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        Resources.Culture = CultureInfo.CurrentUICulture;
        var page = this._context.Render<VipManager>();
        page.WaitForAssertion(() => Assert.That(page.FindAll("#vip-account"), Has.Count.EqualTo(1)));
        page.Find("#vip-account").Change("ann");
        page.Find("#vip-reason").Change("Compensation");
        page.Find("#vip-review-grant").Click();
        page.WaitForAssertion(() => Assert.That(page.FindAll("#vip-confirm"), Has.Count.EqualTo(1)));
        this._audit.RejectCompletion = true;
        page.Find("#vip-confirm").Click();
        page.WaitForAssertion(() => Assert.That(page.FindAll("#vip-retry"), Has.Count.EqualTo(1)));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        var until = this._account.AttributeGrants.First().ValidUntil;
        this._audit.RejectCompletion = false;
        page.Find("#vip-retry").Click();
        page.WaitForAssertion(() => Assert.That(page.FindAll("#vip-confirm"), Has.Count.EqualTo(1)));
        page.Find("#vip-confirm").Click();
        page.WaitForAssertion(() => Assert.That(page.Markup, Does.Contain(Resources.VipManagerAppliedGrant)));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(this._account.AttributeGrants.Select(grant => grant.ValidUntil), Is.All.EqualTo(until));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
    }

    /// <summary>The route requires administrator access independently of the operation service.</summary>
    [Test]
    public void RequiresAdministrator()
        => Assert.That(typeof(VipManager).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Select(attribute => attribute.Policy), Does.Contain(AdminPolicies.Administrator));
}
