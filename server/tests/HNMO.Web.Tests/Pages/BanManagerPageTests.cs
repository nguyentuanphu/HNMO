// <copyright file="BanManagerPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Threading;
using Bunit;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Moderation;
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

/// <summary>Renders the bilingual Ban/Mute Manager page and verifies persistence, salted hashing, search, and lifting.</summary>
[TestFixture]
[NonParallelizable]
public class BanManagerPageTests
{
    private BunitContext _context = null!;
    private CultureInfo _previousCulture = null!;
    private CultureInfo? _previousResource = null!;
    private InMemoryHnmoPlayerStateRowBackend _rows = null!;
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _game = null!;
    private Account _account = null!;
    private readonly List<ActivityLogEntry> _activities = [];

    /// <summary>Creates the isolated persistence graph and BanAdminOperations.</summary>
    /// <returns>Setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._previousCulture = CultureInfo.CurrentUICulture;
        this._previousResource = Resources.Culture;
        this._provider = new InMemoryPersistenceContextProvider();
        using var setup = this._provider.CreateNewContext();
        this._game = setup.CreateNew<GameConfiguration>();
        await setup.SaveChangesAsync().ConfigureAwait(false);

        using var players = this._provider.CreateNewPlayerContext(this._game);
        this._account = players.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.State = AccountState.Normal;
        await players.SaveChangesAsync().ConfigureAwait(false);

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(value => value.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._game);
        var login = new Mock<ILoginServer>();
        login.Setup(v => v.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte>());
        login.Setup(v => v.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);

        this._rows = new InMemoryHnmoPlayerStateRowBackend();
        this._activities.Clear();
        var executor = AdminOperationTestServices.CreateExecutor();
        var ops = new BanAdminOperations(this._provider, source.Object, login.Object, executor, TimeProvider.System, this._rows, this._activities.Add);

        this._context = new();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddLogging();
        this._context.Services.AddSingleton(ops);
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>Restores culture and disposes rendered components.</summary>
    /// <returns>Cleanup task.</returns>
    [TearDown]
    public async Task TearDownAsync()
    {
        await this._context.DisposeAsync().ConfigureAwait(false);
        CultureInfo.CurrentUICulture = this._previousCulture;
        Resources.Culture = this._previousResource;
    }

    /// <summary>EN and VI render PageHelp, hover hints, apply a timed ban and salted network ban, and lift them cleanly.</summary>
    /// <param name="language">Admin UI culture.</param>
    [TestCase("en")]
    [TestCase("vi")]
    public async Task RendersHelpHintsIssuesAndLiftsBanAsync(string language)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        Resources.Culture = CultureInfo.CurrentUICulture;
        var page = this._context.Render<BanManager>();
        page.WaitForAssertion(() => Assert.That(page.Find("h1").TextContent, Is.EqualTo(language == "vi" ? "Quản lý Khóa & Cấm chat" : "Bans & Mutes Manager")));

        page.Find(".page-help button").Click();
        Assert.That(page.Find(".alert-info").TextContent, Does.Contain("Sydney"));
        Assert.That(page.FindAll("input,button,select").All(c => !string.IsNullOrWhiteSpace(c.GetAttribute("title"))), Is.True);

        page.Find("#ban-target").Change("ann");
        page.Find("#ban-minutes").Change("30");
        page.Find("#ban-reason-en").Change("Macro abuse");
        page.Find("#ban-reason-vi").Change("Dùng macro");
        page.Find("#ban-audit-reason").Change("Ticket #42");
        page.Find("#ban-issue-btn").Click();

        page.WaitForAssertion(() => Assert.That(page.FindAll(".unban-btn"), Has.Count.EqualTo(1)));
        using (var verify = this._provider.CreateNewPlayerContext(this._game))
        {
            var saved = (await verify.GetAccountByLoginNameAsync("ann").ConfigureAwait(false))!;
            Assert.That(saved.State, Is.EqualTo(AccountState.TemporarilyBanned));
        }

        page.Find("tbody input").Change("Appeal approved");
        page.Find(".unban-btn").Click();

        page.WaitForAssertion(() => Assert.That(page.FindAll(".unban-btn"), Is.Empty));
        using (var verify = this._provider.CreateNewPlayerContext(this._game))
        {
            var restored = (await verify.GetAccountByLoginNameAsync("ann").ConfigureAwait(false))!;
            Assert.That(restored.State, Is.EqualTo(AccountState.Normal));
        }

        Assert.That(this._activities, Has.Count.EqualTo(2));
    }

    /// <summary>Route requires Operator policy.</summary>
    [Test]
    public void RequiresOperator()
        => Assert.That(typeof(BanManager).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Select(a => a.Policy), Does.Contain(AdminPolicies.Operator));
}
