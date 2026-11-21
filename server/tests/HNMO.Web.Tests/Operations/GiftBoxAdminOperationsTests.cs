// <copyright file="GiftBoxAdminOperationsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Operations;

using System.Text.Json.Nodes;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.InMemory;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Tests.Mcp;
using Moq;

/// <summary>Checks immutable reviewed mail sends at the real authorization and audit boundary.</summary>
[TestFixture]
public class GiftBoxAdminOperationsTests
{
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _game = null!;
    private InMemoryHnmoPlayerStateRowBackend _rows = null!;
    private InMemoryAdminAuditRepository _audit = null!;
    private GiftBoxAdminOperations _operations = null!;
    private AdminActor _actor = null!;
    private Account _ann = null!;
    private Account _bob = null!;
    private readonly List<ActivityLogEntry> _activities = [];
    private bool _rejectRequested;
    private bool _rejectQueued;

    /// <summary>Creates isolated accounts, real confirmation/audit services and a controllable activity sink.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._provider = new();
        using var setup = this._provider.CreateNewContext();
        this._game = setup.CreateNew<GameConfiguration>();
        await setup.SaveChangesAsync().ConfigureAwait(false);
        this._ann = await this.AddAccountAsync("a,b;c").ConfigureAwait(false);
        this._bob = await this.AddAccountAsync("bob").ConfigureAwait(false);
        var configurations = new Mock<IDataSource<GameConfiguration>>();
        configurations.Setup(source => source.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._game);
        var login = new Mock<ILoginServer>();
        login.Setup(server => server.GetSnapshotAsync()).ReturnsAsync(new Dictionary<string, byte> { ["bob"] = 1 });
        this._rows = new();
        this._audit = new();
        this._actor = new("host-id", "Host", "panel", [AdminRoles.Administrator]);
        this._activities.Clear();
        this._rejectRequested = false;
        this._rejectQueued = false;
        var clock = new TestClock();
        this._operations = new(this._provider, configurations.Object, login.Object, AdminOperationTestServices.CreateExecutor(audit: this._audit, actor: () => this._actor), new(clock), new() { ReadOnly = false }, clock, this._rows, entry =>
        {
            var status = JsonNode.Parse(entry.Details)?["status"]?.GetValue<string>();
            if ((this._rejectRequested && status == "Requested") || (this._rejectQueued && status == "Queued"))
            {
                throw new InvalidOperationException("Injected activity failure.");
            }

            this._activities.Add(entry);
        });
    }

    /// <summary>A preview writes nothing; applying sends only owed letters and keeps host reasons private.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PreviewThenApplyPreservesExactLoginAndAuditsAsync()
    {
        var draft = Draft();
        var preview = await this.PreviewAsync(draft).ConfigureAwait(false);
        Assert.That(this._rows.CountRows(), Is.Zero);
        Assert.That(this._activities, Is.Empty);
        Assert.That((await this._audit.ReadAsync(0, 20).ConfigureAwait(false)).Total, Is.Zero);
        await this.ApplyAsync(draft, preview).ConfigureAwait(false);
        var letter = new GiftBoxDispatch(this._rows).Read(this._ann.GetId()).Single();
        Assert.That(letter.Reward.Coins, Is.EqualTo(10));
        Assert.That(this._ann.WCoinC, Is.Zero);
        Assert.That(new GiftBoxDispatch(this._rows).Read(this._bob.GetId()), Is.Empty);
        Assert.That(System.Text.Json.JsonSerializer.Serialize(letter), Does.Not.Contain("private reason").And.Not.Contain("host-id"));
        Assert.That(this._activities, Has.Count.EqualTo(2));
        Assert.That((await this._audit.ReadAsync(0, 20).ConfigureAwait(false)).Items.Single().Operation, Is.EqualTo("gift_box_send"));
    }

    /// <summary>Exact newline lists deduplicate accounts without interpreting punctuation as separators.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PastedListIsExactAndUnknownAccountRefusesWholeSendAsync()
    {
        var draft = Draft() with { Audience = GiftBoxAudienceKind.List, Logins = "a,b;c\nbob\nbob" };
        await this.ApplyAsync(draft, await this.PreviewAsync(draft).ConfigureAwait(false)).ConfigureAwait(false);
        Assert.That(this._rows.CountRows(GiftBoxDispatch.LetterFeature), Is.EqualTo(2));
        var bad = Draft() with { Audience = GiftBoxAudienceKind.List, Logins = "bob\nmissing" };
        Assert.ThrowsAsync<AdminOperationException>(async () => await this.PreviewAsync(bad).ConfigureAwait(false));
        Assert.That(this._rows.CountRows(GiftBoxDispatch.LetterFeature), Is.EqualTo(2));
    }

    /// <summary>Recent audiences use observed history or live login state, never guessed timestamps.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RecentAudienceUsesObservedActivityAndOnlineAccountsAsync()
    {
        new GiftBoxActivity(this._rows).Observe(this._ann.GetId(), TestClock.Now.UtcDateTime.AddDays(-2));
        var draft = Draft() with { Audience = GiftBoxAudienceKind.Everyone, Logins = string.Empty, ActiveDays = 1 };
        await this.ApplyAsync(draft, await this.PreviewAsync(draft).ConfigureAwait(false)).ConfigureAwait(false);
        Assert.That(new GiftBoxDispatch(this._rows).Read(this._ann.GetId()), Is.Empty);
        Assert.That(new GiftBoxDispatch(this._rows).Read(this._bob.GetId()), Has.Count.EqualTo(1));
    }

    /// <summary>Stable retries retain the original cohort even when new accounts appear.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReplayFreezesEveryoneAndConsumesEachPreviewOnceAsync()
    {
        var draft = Draft() with { Audience = GiftBoxAudienceKind.Everyone, Logins = string.Empty };
        var preview = await this.PreviewAsync(draft).ConfigureAwait(false);
        await this.ApplyAsync(draft, preview).ConfigureAwait(false);
        Assert.ThrowsAsync<AdminOperationException>(async () => await this.ApplyAsync(draft, preview).ConfigureAwait(false));
        var newcomer = await this.AddAccountAsync("new").ConfigureAwait(false);
        var result = await this.ApplyAsync(draft, await this.PreviewAsync(draft).ConfigureAwait(false)).ConfigureAwait(false);
        Assert.That(result["status"]!.GetValue<string>(), Is.EqualTo("AlreadyQueued"));
        Assert.That(new GiftBoxDispatch(this._rows).Read(newcomer.GetId()), Is.Empty);
        Assert.That(this._rows.CountRows(GiftBoxDispatch.LetterFeature), Is.EqualTo(2));
    }

    /// <summary>A changed host or reward cannot reuse an earlier confirmation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ConfirmationBindsActorAndReviewedInputAsync()
    {
        var draft = Draft();
        var preview = await this.PreviewAsync(draft).ConfigureAwait(false);
        Assert.ThrowsAsync<AdminOperationException>(async () => await this.ApplyAsync(draft with { MessageVi = "Nội dung khác" }, preview).ConfigureAwait(false));
        this._actor = this._actor with { Id = "another-host" };
        Assert.ThrowsAsync<AdminOperationException>(async () => await this.ApplyAsync(draft, preview).ConfigureAwait(false));
        Assert.That(this._rows.CountRows(), Is.Zero);
    }

    /// <summary>Both required audit writes precede delivery.</summary>
    /// <param name="activityFailure">Whether to reject the activity log rather than the central audit.</param>
    /// <returns>The test task.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task MissingAuditPreventsSendAsync(bool activityFailure)
    {
        var draft = Draft();
        var preview = await this.PreviewAsync(draft).ConfigureAwait(false);
        this._rejectRequested = activityFailure;
        this._audit.RejectWrites = !activityFailure;
        Assert.CatchAsync<Exception>(async () => await this.ApplyAsync(draft, preview).ConfigureAwait(false));
        Assert.That(this._rows.CountRows(), Is.Zero);
    }

    /// <summary>A lost final activity result remains uncertain; retry reconciles exactly one immutable send.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LostFinalActivityKeepsPendingAndRetriesWithoutDuplicatesAsync()
    {
        var draft = Draft();
        var preview = await this.PreviewAsync(draft).ConfigureAwait(false);
        this._rejectQueued = true;
        var error = Assert.ThrowsAsync<AdminOperationException>(async () => await this.ApplyAsync(draft, preview).ConfigureAwait(false));
        Assert.That(error!.Kind, Is.EqualTo(AdminOperationErrorKind.Uncertain));
        Assert.That((await this._audit.ReadAsync(0, 20).ConfigureAwait(false)).Items.Single().Outcome, Is.EqualTo("Pending"));
        this._rejectQueued = false;
        await this.ApplyAsync(draft, await this.PreviewAsync(draft).ConfigureAwait(false)).ConfigureAwait(false);
        Assert.That(this._rows.CountRows(GiftBoxDispatch.LetterFeature), Is.EqualTo(1));
    }

    /// <summary>Authorization is enforced inside the service before lookup or preview.</summary>
    [Test]
    public void ViewerCannotPreview()
    {
        this._actor = this._actor with { Roles = [AdminRoles.Viewer] };
        Assert.ThrowsAsync<AdminOperationException>(async () => await this.PreviewAsync(Draft()).ConfigureAwait(false));
        Assert.That(this._rows.CountRows(), Is.Zero);
    }

    private static GiftBoxSendDraft Draft() => new(Guid.NewGuid(), GiftBoxAudienceKind.Account, "a,b;c", null, "Gift", "Quà", "Thank you.", "Cảm ơn bạn.", new() { Coins = 10 }, null);

    private ValueTask<JsonObject> PreviewAsync(GiftBoxSendDraft draft) => this._operations.SendAsync(draft, "private reason", false, null, default);

    private ValueTask<JsonObject> ApplyAsync(GiftBoxSendDraft draft, JsonObject preview) => this._operations.SendAsync(draft, "private reason", true, preview["confirmationToken"]!.GetValue<string>(), default);

    private async ValueTask<Account> AddAccountAsync(string login)
    {
        using var context = this._provider.CreateNewPlayerContext(this._game);
        var account = context.CreateNew<Account>();
        account.LoginName = login;
        await context.SaveChangesAsync().ConfigureAwait(false);
        return account;
    }

    private sealed class TestClock : TimeProvider
    {
        public static DateTimeOffset Now { get; } = new(2026, 11, 18, 6, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
