// <copyright file="AccountSafetyPageTests.cs" company="HNMO">
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
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Tests.Mcp;
using HNMO.Web.Shared.Components.Toast;
using AccountSafetyPage = HNMO.Web.AdminPanel.Pages.AccountSafety;

/// <summary>
/// Tests for the admin panel's "Account safety" page.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class AccountSafetyPageTests
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(10);

    private BunitContext _context = null!;
    private CultureInfo _previousUiCulture = null!;
    private PlugInConfiguration _plugin = null!;
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _configuration = null!;
    private string _file = null!;

    /// <summary>
    /// Sets up the page with a store in its own file and an account with a second password.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        AccountSafetyEngine.WorkFactor = 4;
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-safety-page-{Guid.NewGuid():N}.json");
        this._provider = new InMemoryPersistenceContextProvider();
        using (var setup = this._provider.CreateNewContext())
        {
            this._configuration = setup.CreateNew<GameConfiguration>();
            this._plugin = setup.CreateNew<PlugInConfiguration>();
            this._plugin.TypeId = typeof(AccountSafetyPlugIn).GUID;
            this._plugin.IsActive = true;
            this._plugin.CustomConfiguration = JsonSerializer.Serialize(new AccountSafetyConfiguration { FileName = this._file });
            this._configuration.PlugInConfigurations.Add(this._plugin);
            await setup.SaveChangesAsync().ConfigureAwait(false);
        }

        using (var players = this._provider.CreateNewPlayerContext(this._configuration))
        {
            var account = players.CreateNew<Account>();
            account.LoginName = "ann";
            account.VaultPassword = AccountSafetyEngine.HashPin("1234");
            await players.SaveChangesAsync().ConfigureAwait(false);
        }

        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._configuration);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IPlayerStateChangedPlugIn, AccountSafetyPlugIn>();
        var writes = new PluginWriteOperations(this._provider, source.Object, manager, AdminOperationTestServices.CreateExecutor(), new AdminChangeConfirmation(TimeProvider.System), new AdminWriteSettings { ReadOnly = false });
        var login = new Mock<ILoginServer>();
        login.Setup(l => l.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(true);
        var servers = new Mock<IServerProvider>();
        servers.Setup(s => s.Servers).Returns(new List<IManageableServer>());

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton<IPersistenceContextProvider>(this._provider);
        this._context.Services.AddSingleton(source.Object);
        this._context.Services.AddSingleton(login.Object);
        this._context.Services.AddSingleton(servers.Object);
        this._context.Services.AddSingleton(writes);
        this._context.Services.AddSingleton(Mock.Of<IToastService>());
        this._context.Services.AddSingleton(Mock.Of<ILogger<AccountSafetyPage>>());
        this._context.ComponentFactories.AddStub<HNMO.Web.Shared.Components.Breadcrumb>();
    }

    /// <summary>
    /// Restores the culture, disposes the test context and deletes the file.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._context.Dispose();
        CultureInfo.CurrentUICulture = this._previousUiCulture;
        File.Delete(this._file);
    }

    /// <summary>
    /// The switches and numbers are saved into the plug-in's settings, numbers kept in their ranges.
    /// </summary>
    [Test]
    public void TheOptionsAreEditedAndSaved()
    {
        var cut = this._context.Render<AccountSafetyPage>();
        cut.WaitForState(() => cut.FindAll("#safety-save").Count == 1, LoadTimeout);
        cut.Find("#safety-trade").Change(false);
        cut.Find("#safety-valuable").Change(true);
        cut.Find("#safety-tries").Change("3");
        cut.Find("#safety-wait").Change("48");
        cut.Find("#safety-login-days").Change("0");
        cut.Find("#safety-notify").Change(false);
        cut.Find("#safety-save").Click();

        cut.WaitForState(() => Saved()?.UnlockWaitHours == 48, LoadTimeout);
        var saved = Saved()!;
        Assert.That((saved.RequireForTrade, saved.RequireForValuableItems, saved.MaxWrongTries, saved.NotifyNewAddress), Is.EqualTo((false, true, 3, false)));
        Assert.That(saved.LoginHistoryDays, Is.EqualTo(1), "kept in its range");
        Assert.That(saved.FileName, Is.EqualTo(this._file));
    }

    /// <summary>
    /// An account's view shows its full logins, trades and locks (in English and Vietnamese); the admin unlocks an
    /// item, clears the lockout and resets the second password.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="loginsHeading">The logins heading.</param>
    [TestCase("en", "Logins")]
    [TestCase("vi", "Đăng nhập")]
    public void AnAccountIsShownAndHelped(string culture, string loginsHeading)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var engine = new AccountSafetyEngine(new AccountSafetyConfiguration(), AccountSafetyStore.Open(this._file));
        var now = DateTime.UtcNow;
        engine.RecordLogin("ann", "203.0.113.7", "game", true, now.AddHours(-1));
        engine.RecordLogin("ann", "198.51.100.9", "website", false, now);
        engine.RecordTrade("ann", new SafetyTradeEntry { Character = "AnnKnight", Partner = "Bob", PartnerLogin = "bob", Gave = { "Kris +7" }, ZenGot = 1000 }, now);
        var item = Guid.NewGuid();
        engine.Lock("ann", item, "Kris +7", "AnnKnight", now);
        for (var i = 0; i < 5; i++)
        {
            engine.CheckPin("ann", AccountSafetyEngine.HashPin("1234"), "0000", now);
        }

        var cut = this._context.Render<AccountSafetyPage>();
        cut.WaitForState(() => cut.FindAll("#safety-account-show").Count == 1, LoadTimeout);
        cut.Find("#safety-account-name").Change("ann");
        cut.Find("#safety-account-show").Click();
        cut.WaitForState(() => cut.FindAll("#safety-reset-pin").Count == 1 && !cut.Find("#safety-reset-pin").HasAttribute("disabled"), LoadTimeout);

        Assert.That(cut.Markup, Does.Contain(loginsHeading));
        Assert.That(cut.FindAll(".safety-login").Select(r => r.TextContent), Has.Some.Contains("198.51.100.9"), "the whole address");
        Assert.That(cut.Find(".safety-trade").TextContent, Does.Contain("Bob (bob)").And.Contain("1,000 zen").Or.Contain("1.000 zen"));
        Assert.That(cut.Find(".safety-lock").TextContent, Does.Contain("Kris +7"));

        cut.Find(".safety-unlock").Click();
        Assert.That(engine.IsLocked("ann", item, DateTime.UtcNow), Is.False);
        cut.Find("#safety-clear-lockout").Click();
        Assert.That(engine.LockedOutUntil("ann", DateTime.UtcNow), Is.Null);

        cut.Find("#safety-reset-pin").Click();
        cut.WaitForState(() => cut.FindAll("#safety-message").Count == 1 && cut.Find("#safety-reset-pin").HasAttribute("disabled"), LoadTimeout);
        using var context = this._provider.CreateNewPlayerContext(this._configuration);
        Assert.That(context.GetAccountByLoginNameAsync("ann", default).AsTask().Result!.VaultPassword, Is.Empty);
    }

    private AccountSafetyConfiguration? Saved() =>
        this._plugin.CustomConfiguration is { } json ? JsonSerializer.Deserialize<AccountSafetyConfiguration>(json) : null;
}
