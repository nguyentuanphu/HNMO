// <copyright file="ChatLogPageTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Pages;

using System.Globalization;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Views;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.Tests.AdminAuth;
using HNMO.Web.Tests.Mcp;
using ChatLogBuffer = HNMO.GameLogic.PlugIns.Chat.ChatLog;
using ChatLogPage = HNMO.Web.AdminPanel.Pages.ChatLog;

/// <summary>
/// Tests for the admin panel's <see cref="ChatLogPage"/>.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ChatLogPageTests
{
    private CultureInfo _previousUiCulture = null!;
    private BunitContext _context = null!;
    private ChatLogBuffer _log = null!;
    private InMemoryPersistenceContextProvider _persistence = null!;

    /// <summary>
    /// Sets up the page's services and a chat log with three lines.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

        this._log = new ChatLogBuffer(10);
        var time = new DateTime(2026, 11, 14, 1, 0, 0, DateTimeKind.Utc);
        this._log.Add(time, 0, "Lorencia", "Alice", Guid.NewGuid(), ChatMessageType.Normal, null, "hello all");
        this._log.Add(time.AddSeconds(5), 0, "Noria", "Bob", Guid.NewGuid(), ChatMessageType.Whisper, "Alice", "psst");
        this._log.Add(time.AddSeconds(9), 1, "Devias", "Carl", Guid.Empty, ChatMessageType.Guild, null, "@guild meeting");

        this._persistence = new InMemoryPersistenceContextProvider();
        var source = new Mock<IDataSource<GameConfiguration>>();
        source.Setup(s => s.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HNMO.Persistence.BasicModel.GameConfiguration());

        this._context = new BunitContext();
        this._context.JSInterop.Mode = JSRuntimeMode.Loose;
        this._context.Services.AddSingleton(AdminOperationTestServices.CreateExecutor());
        this._context.Services.AddSingleton<IPersistenceContextProvider>(this._persistence);
        this._context.Services.AddSingleton(source.Object);
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
    /// The lines show newest first with Sydney time, and the search filters by sender or text.
    /// </summary>
    [Test]
    public void ShowsLinesNewestFirstAndSearches()
    {
        this._context.AddAdministratorAuthorization();
        var page = this.Render();

        var rows = page.FindAll("[data-testid=chat-log-table] tbody tr");
        Assert.That(rows.Select(r => r.Children[3].TextContent), Is.EqualTo(new[] { "Carl", "Bob", "Alice" }));
        Assert.That(rows[2].Children[0].TextContent, Is.EqualTo("2026-11-14 12:00:00"), "11 a.m. summer time in Sydney is UTC+11.");
        Assert.That(rows[1].Children[4].TextContent, Is.EqualTo("Whisper → Alice"));

        page.Find("[data-testid=chat-log-search]").Input("PSST");
        rows = page.FindAll("[data-testid=chat-log-table] tbody tr");
        Assert.That(rows.Select(r => r.Children[3].TextContent), Is.EqualTo(new[] { "Bob" }));

        page.Find("[data-testid=chat-log-search]").Input("alice");
        Assert.That(page.FindAll("[data-testid=chat-log-table] tbody tr").Select(r => r.Children[3].TextContent), Is.EqualTo(new[] { "Bob", "Alice" }));
    }

    /// <summary>
    /// An operator mutes the sender of a line: the account's chat ban is saved.
    /// </summary>
    [Test]
    public async Task MuteSetsChatBanOfAccountAsync()
    {
        using (var context = this._persistence.CreateNewPlayerContext(new HNMO.Persistence.BasicModel.GameConfiguration()))
        {
            var account = context.CreateNew<Account>();
            account.LoginName = "alice";
            var character = context.CreateNew<Character>();
            character.Name = "Alice";
            account.Characters.Add(character);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        this._context.AddAdministratorAuthorization();
        var page = this.Render();
        var aliceRow = page.FindAll("[data-testid=chat-log-table] tbody tr").Single(r => r.Children[3].TextContent == "Alice");
        aliceRow.QuerySelectorAll("button")[1].Click();

        page.WaitForAssertion(() => Assert.That(page.Find("[role=status]").TextContent, Does.Contain("Alice is muted until")));
        using var check = this._persistence.CreateNewPlayerContext(new HNMO.Persistence.BasicModel.GameConfiguration());
        var muted = await check.GetAccountByCharacterNameAsync("Alice").ConfigureAwait(false);
        Assert.That(muted!.ChatBanUntil, Is.GreaterThan(DateTime.UtcNow.AddMinutes(55)).And.LessThan(DateTime.UtcNow.AddMinutes(65)));
    }

    /// <summary>
    /// A viewer reads the log but gets no mute buttons.
    /// </summary>
    [Test]
    public void ViewerHasNoMuteButtons()
    {
        var authorization = this._context.AddAuthorization();
        authorization.SetAuthorized("viewer");
        authorization.SetRoles(AdminRoles.Viewer);
        authorization.SetPolicies(AdminPolicies.Viewer);
        this._context.Services.AddScoped<AdminPolicyGuard>();

        var page = this.Render();

        Assert.That(page.FindAll("[data-testid=chat-log-table] tbody tr"), Has.Count.EqualTo(3));
        Assert.That(page.FindAll("[data-testid=chat-log-table] button"), Is.Empty);
    }

    private IRenderedComponent<ChatLogPage> Render()
    {
        return this._context.Render<ChatLogPage>(parameters => parameters.Add(p => p.Log, this._log));
    }
}
