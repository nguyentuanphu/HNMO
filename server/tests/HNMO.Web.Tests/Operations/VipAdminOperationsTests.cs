// <copyright file="VipAdminOperationsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.Tests.Operations;

using System.Text.Json.Nodes;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.AdminAuth;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using HNMO.Web.AdminPanel.Auth;
using HNMO.Web.AdminPanel.Operations;
using HNMO.Web.Tests.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Checks host authorization, exact account reservations, preview binding and durable audit before VIP mutation.</summary>
[TestFixture]
public class VipAdminOperationsTests
{
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _game = null!;
    private Account _account = null!;
    private PlugInConfiguration _plugin = null!;
    private VipStateStore _state = null!;
    private Mock<ILoginServer> _login = null!;
    private InMemoryAdminAuditRepository _audit = null!;
    private VipAdminOperations _operations = null!;
    private AdminActor _actor = null!;
    private readonly List<ActivityLogEntry> _activities = [];
    private bool _rejectActivity;
    private bool _rejectAppliedActivity;

    /// <summary>Creates an isolated account, real authorization/audit executor and controllable login reservation.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._provider = new();
        using var setup = this._provider.CreateNewContext();
        this._game = setup.CreateNew<GameConfiguration>();
        foreach (var definition in new[] { Stats.IsVip, Stats.BonusExperienceRate, Stats.MoneyAmountRate })
        {
            this._game.Attributes.Add(setup.CreateNew<AttributeDefinition>(definition.Id, definition.Designation, definition.Description));
        }

        this._plugin = setup.CreateNew<PlugInConfiguration>();
        this._plugin.TypeId = typeof(VipPlugIn).GUID;
        this._plugin.IsActive = true;
        this._plugin.SetConfiguration(new VipConfiguration(), null);
        await setup.SaveChangesAsync().ConfigureAwait(false);
        using var context = this._provider.CreateNewPlayerContext(this._game);
        this._account = context.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.WCoinC = 1000;
        await context.SaveChangesAsync().ConfigureAwait(false);
        var configurations = new Mock<IDataSource<GameConfiguration>>();
        configurations.Setup(source => source.GetOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(this._game);
        this._state = VipStateStore.CreateInMemory();
        this._login = new();
        this._login.Setup(login => login.TryReserveAccountAsync("ann", It.IsAny<int>())).ReturnsAsync(true);
        this._login.Setup(login => login.ReleaseAccountReservationAsync("ann", It.IsAny<int>())).Returns(ValueTask.CompletedTask);
        this._audit = new();
        this._actor = new("host-id", "Host", "panel", [AdminRoles.Administrator]);
        var executor = AdminOperationTestServices.CreateExecutor(audit: this._audit, actor: () => this._actor);
        var clock = new TestClock();
        this._activities.Clear();
        this._rejectActivity = false;
        this._rejectAppliedActivity = false;
        this._operations = new(this._provider, configurations.Object, this._login.Object, executor, new(clock), new() { ReadOnly = false }, NullLogger<VipAdminOperations>.Instance, clock, this._state, entry =>
        {
            if (this._rejectActivity || (this._rejectAppliedActivity && JsonNode.Parse(entry.Details)?["status"]?.GetValue<string>() == "Applied"))
            {
                throw new InvalidOperationException("injected activity write failure");
            }

            this._activities.Add(entry);
        });
    }

    /// <summary>Preview spends nothing; apply reserves the exact stored login and records both audit trails.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PreviewThenGrantUsesReservationAndAuditAsync()
    {
        var operation = Guid.NewGuid();
        var preview = await this.PreviewAsync(operation).ConfigureAwait(false);
        Assert.That(this._account.AttributeGrants, Is.Empty);
        Assert.That(this._activities, Is.Empty);
        Assert.That((await this._audit.ReadAsync(0, 20).ConfigureAwait(false)).Total, Is.Zero);
        var result = await this.ApplyAsync(operation, preview).ConfigureAwait(false);
        Assert.That(result["status"]!.GetValue<string>(), Is.EqualTo("Applied"));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        this._login.Verify(login => login.TryReserveAccountAsync("ann", It.Is<int>(id => id > byte.MaxValue)), Times.Once);
        this._login.Verify(login => login.ReleaseAccountReservationAsync("ann", It.Is<int>(id => id > byte.MaxValue)), Times.Once);
        Assert.That(this._activities, Has.Count.EqualTo(2));
        Assert.That(this._activities.All(entry => entry.Kind == ActivityKind.Administration && entry.Account == "ann" && entry.Details.Contains(operation.ToString(), StringComparison.Ordinal)), Is.True);
        Assert.That((await this._audit.ReadAsync(0, 20).ConfigureAwait(false)).Items.Single().Operation, Is.EqualTo("vip_grant"));
    }

    /// <summary>The subscription index loads identified accounts without uppercasing their case-sensitive persistent login.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ListAndExactLookupUseProvedAccountIdentityAsync()
    {
        var operation = Guid.NewGuid();
        await this.ApplyAsync(operation, await this.PreviewAsync(operation).ConfigureAwait(false)).ConfigureAwait(false);
        var indexed = await this._operations.ReadAsync(null, default).ConfigureAwait(false);
        var exact = await this._operations.ReadAsync("ann", default).ConfigureAwait(false);
        Assert.That(indexed.Accounts.Single().LoginName, Is.EqualTo("ann"));
        Assert.That(exact.Accounts.Single().LoginName, Is.EqualTo("ann"));
        Assert.That(indexed.Configuration.Enabled, Is.False);
        Assert.That(indexed.Accounts.Single().UntilUtc, Is.EqualTo(TestClock.Now.UtcDateTime.AddDays(7)));
    }

    /// <summary>The settings which the data initialization stored (every object with an empty "$id", lists as "$values") are readable.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task SettingsStoredWithEmptyReferenceIdsAreReadableAsync()
    {
        using (var context = this._provider.CreateNewContext())
        {
            var stored = (await context.GetAsync<PlugInConfiguration>().ConfigureAwait(false)).Single(c => c.TypeId == typeof(VipPlugIn).GUID);
            stored.CustomConfiguration = WithEmptyReferenceIds(System.Text.Json.JsonSerializer.SerializeToNode(new VipConfiguration()))!.ToJsonString();
            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        var snapshot = await this._operations.ReadAsync(null, default).ConfigureAwait(false);
        Assert.That(snapshot.Configuration.Levels, Has.Count.EqualTo(new VipConfiguration().Levels.Count));
    }

    private static JsonNode? WithEmptyReferenceIds(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var result = new JsonObject { ["$id"] = string.Empty };
                foreach (var (key, value) in obj)
                {
                    result[key] = WithEmptyReferenceIds(value?.DeepClone());
                }

                return result;
            case JsonArray array:
                var values = new JsonArray();
                foreach (var item in array)
                {
                    values.Add(WithEmptyReferenceIds(item?.DeepClone()));
                }

                return new JsonObject { ["$id"] = string.Empty, ["$values"] = values };
            default:
                return node;
        }
    }

    /// <summary>An online account cannot be changed and a failed reservation cannot clear another server's ownership.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task OnlineAccountIsRefusedBeforeAuditOrGrantAsync()
    {
        var operation = Guid.NewGuid();
        var preview = await this.PreviewAsync(operation).ConfigureAwait(false);
        this._login.Setup(login => login.TryReserveAccountAsync("ann", It.IsAny<int>())).ReturnsAsync(false);
        Assert.That((await this.ApplyAsync(operation, preview).ConfigureAwait(false))["status"]!.GetValue<string>(), Is.EqualTo("Online"));
        Assert.That(this._account.AttributeGrants, Is.Empty);
        Assert.That(this._activities, Is.Empty);
        Assert.That((await this._audit.ReadAsync(0, 20).ConfigureAwait(false)).Total, Is.Zero);
    }

    /// <summary>The operation service itself denies a low-role caller, independent of Razor route authorization.</summary>
    [Test]
    public void LowRoleCannotReadOrPreview()
    {
        this._actor = this._actor with { Roles = [AdminRoles.Viewer] };
        Assert.ThrowsAsync<AdminOperationException>(async () => await this._operations.ReadAsync(null, default).ConfigureAwait(false));
        Assert.ThrowsAsync<AdminOperationException>(async () => await this.PreviewAsync(Guid.NewGuid()).ConfigureAwait(false));
        this._login.Verify(login => login.TryReserveAccountAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>Audit failures prevent account mutation, including the activity-log write before the grant.</summary>
    /// <param name="activityFailure">Whether the player activity sink fails rather than the administration audit.</param>
    /// <returns>The test task.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task AuditFailurePreventsGrantAsync(bool activityFailure)
    {
        var operation = Guid.NewGuid();
        var preview = await this.PreviewAsync(operation).ConfigureAwait(false);
        this._rejectActivity = activityFailure;
        this._audit.RejectWrites = !activityFailure;
        if (activityFailure)
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () => await this.ApplyAsync(operation, preview).ConfigureAwait(false));
        }
        else
        {
            Assert.ThrowsAsync<AdminOperationException>(async () => await this.ApplyAsync(operation, preview).ConfigureAwait(false));
        }
        Assert.That(this._account.AttributeGrants, Is.Empty);
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        this._login.Verify(login => login.ReleaseAccountReservationAsync("ann", It.IsAny<int>()), Times.Once);
    }

    /// <summary>Used previews fail closed; a fresh preview of the same stable operation confirms the original grant.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RepreviewSameOperationDoesNotGrantTwiceAsync()
    {
        var operation = Guid.NewGuid();
        var preview = await this.PreviewAsync(operation).ConfigureAwait(false);
        await this.ApplyAsync(operation, preview).ConfigureAwait(false);
        Assert.ThrowsAsync<AdminOperationException>(async () => await this.ApplyAsync(operation, preview).ConfigureAwait(false));
        var retry = await this.PreviewAsync(operation).ConfigureAwait(false);
        await this.ApplyAsync(operation, retry).ConfigureAwait(false);
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(this._account.AttributeGrants.Select(grant => grant.ValidUntil), Is.All.EqualTo(TestClock.Now.UtcDateTime.AddDays(7)));
    }

    /// <summary>A changed benefit configuration invalidates the reviewed preview before any mutation.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ChangedBenefitsRequireNewPreviewAsync()
    {
        var operation = Guid.NewGuid();
        var preview = await this.PreviewAsync(operation).ConfigureAwait(false);
        var configuration = new VipConfiguration();
        configuration.Levels[0].ExperiencePercent++;
        this._plugin.SetConfiguration(configuration, null);
        Assert.ThrowsAsync<AdminOperationException>(async () => await this.ApplyAsync(operation, preview).ConfigureAwait(false));
        Assert.That(this._account.AttributeGrants, Is.Empty);
        Assert.That(this._activities, Is.Empty);
    }

    /// <summary>Grant and removal are separate attributed, replay-safe host operations.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RemovalUsesSameOfflineAuditBoundaryAsync()
    {
        var grant = Guid.NewGuid();
        await this.ApplyAsync(grant, await this.PreviewAsync(grant).ConfigureAwait(false)).ConfigureAwait(false);
        var removal = Guid.NewGuid();
        var preview = await this.PreviewAsync(removal, true).ConfigureAwait(false);
        var result = await this.ApplyAsync(removal, preview, true).ConfigureAwait(false);
        Assert.That(result["status"]!.GetValue<string>(), Is.EqualTo("Applied"));
        Assert.That(this._account.GetAccountAttributeValue(Stats.IsVip, TestClock.Now.UtcDateTime), Is.Zero);
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(this._activities, Has.Count.EqualTo(4));
    }

    /// <summary>A failed activity outcome after commit leaves audit Pending; replay reconciles the original grant.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LostActivityOutcomeKeepsAuditPendingAndDoesNotGrantAgainAsync()
    {
        var operation = Guid.NewGuid();
        var preview = await this.PreviewAsync(operation).ConfigureAwait(false);
        this._rejectAppliedActivity = true;
        var exception = Assert.ThrowsAsync<AdminOperationException>(async () => await this.ApplyAsync(operation, preview).ConfigureAwait(false));
        Assert.That(exception!.Kind, Is.EqualTo(AdminOperationErrorKind.Uncertain));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        var until = this._account.AttributeGrants.First().ValidUntil;
        var audits = await this._audit.ReadAsync(0, 20).ConfigureAwait(false);
        Assert.That(audits.Items.Single().Outcome, Is.EqualTo("Pending"));
        this._rejectAppliedActivity = false;
        await this.ApplyAsync(operation, await this.PreviewAsync(operation).ConfigureAwait(false)).ConfigureAwait(false);
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(this._account.AttributeGrants.Select(grant => grant.ValidUntil), Is.All.EqualTo(until));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
    }

    private ValueTask<JsonObject> PreviewAsync(Guid operation, bool remove = false)
        => this._operations.ChangeAsync("ann", operation, remove ? 0 : 1, remove ? 0 : 7, remove, "Compensation", false, null, default);

    private ValueTask<JsonObject> ApplyAsync(Guid operation, JsonObject preview, bool remove = false)
        => this._operations.ChangeAsync("ann", operation, remove ? 0 : 1, remove ? 0 : 7, remove, "Compensation", true, preview["confirmationToken"]!.GetValue<string>(), default);

    private sealed class TestClock : TimeProvider
    {
        public static DateTimeOffset Now { get; } = new(2026, 11, 17, 1, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
