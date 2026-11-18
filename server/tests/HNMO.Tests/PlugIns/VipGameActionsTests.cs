// <copyright file="VipGameActionsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Text.Json;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Persistence;

/// <summary>Checks the real in-world adapter, live attributes and same-wallet replay boundary.</summary>
[TestFixture]
public class VipGameActionsTests
{
    private Player _player = null!;
    private VipGameActions _actions = null!;
    private VipConfiguration _configuration = null!;

    /// <summary>Creates an entered player with identified persisted account and attribute definitions.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        var game = GameContextTestHelper.CreateGameContext();
        using var setup = game.PersistenceContextProvider.CreateNewContext();
        foreach (var attribute in new[] { Stats.IsVip, Stats.BonusExperienceRate, Stats.MoneyAmountRate })
        {
            game.Configuration.Attributes.Add(setup.CreateNew<AttributeDefinition>(attribute.Id, attribute.Designation, attribute.Description));
        }

        this._player = await PlayerTestHelper.CreatePlayerAsync(game).ConfigureAwait(false);
        var account = this._player.PersistenceContext.CreateNew<Account>();
        account.LoginName = "ann";
        account.WCoinC = 1000;
        this._player.Account = account;
        await this._player.PersistenceContext.SaveChangesAsync().ConfigureAwait(false);
        this._configuration = new() { Enabled = true };
        this._actions = new(this._configuration, VipStateStore.CreateInMemory(), new TestClock());
    }

    /// <summary>Releases player timers and persistence.</summary>
    /// <returns>The cleanup task.</returns>
    [TearDown]
    public async Task TearDownAsync() => await this._player.DisposeAsync().ConfigureAwait(false);

    /// <summary>One reviewed game purchase applies actual live XP/zen grants and retry never adds a second debit.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PurchaseRefreshesLiveBenefitsAndReplaysAsync()
    {
        var attributes = this._player.Attributes!;
        var xp = attributes[Stats.BonusExperienceRate];
        var zen = attributes[Stats.MoneyAmountRate];
        var request = await this.SelectionAsync().ConfigureAwait(false);
        using var first = JsonDocument.Parse((await this._actions.HandleAsync(this._player, request).ConfigureAwait(false))!);
        using var replay = JsonDocument.Parse((await this._actions.HandleAsync(this._player, request).ConfigureAwait(false))!);
        Assert.That(first.RootElement.GetProperty("status").GetString(), Is.EqualTo("Applied"));
        Assert.That(replay.RootElement.GetProperty("status").GetString(), Is.EqualTo("Applied"));
        Assert.That(this._player.Account!.WCoinC, Is.EqualTo(900));
        Assert.That(this._player.Account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(this._player.Account.AttributeGrants.Select(grant => grant.ValidUntil).Distinct().Count(), Is.EqualTo(1));
        Assert.That(attributes[Stats.IsVip], Is.EqualTo(1));
        Assert.That(attributes[Stats.BonusExperienceRate], Is.EqualTo(xp + 0.05f).Within(0.00001));
        Assert.That(attributes[Stats.MoneyAmountRate], Is.EqualTo(zen + 0.05f).Within(0.00001));
        var history = await this._player.PersistenceContext.GetCashShopCoinHistoryAsync(this._player.Account.GetId(), 0, 20).ConfigureAwait(false);
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].AppliedAt, Is.Not.Null);
    }

    /// <summary>Daily game claims use the same unique account/Sydney-day pending coin grant as the website.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DailyClaimCreditsOnceThroughNormalCoinApplicationAsync()
    {
        await this._actions.HandleAsync(this._player, await this.SelectionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        using var first = JsonDocument.Parse((await this._actions.HandleAsync(this._player, new("daily")).ConfigureAwait(false))!);
        using var retry = JsonDocument.Parse((await this._actions.HandleAsync(this._player, new("daily")).ConfigureAwait(false))!);
        Assert.That(first.RootElement.GetProperty("status").GetString(), Is.EqualTo("DailyApplied"));
        Assert.That(retry.RootElement.GetProperty("status").GetString(), Is.EqualTo("AlreadyClaimed"));
        Assert.That(this._player.Account!.GoblinPoints, Is.EqualTo(this._configuration.Levels[0].DailyCoins));
        Assert.That(await this._player.PersistenceContext.GetPendingCashShopCoinGrantsAsync(this._player.Account.GetId()).ConfigureAwait(false), Is.Empty);
    }

    /// <summary>Disabling new purchases cannot charge a previously reviewed selection.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DisabledOfferCannotChargeAsync()
    {
        var request = await this.SelectionAsync().ConfigureAwait(false);
        this._configuration.Enabled = false;
        using var result = JsonDocument.Parse((await this._actions.HandleAsync(this._player, request).ConfigureAwait(false))!);
        Assert.That(result.RootElement.GetProperty("status").GetString(), Is.EqualTo("Refused"));
        Assert.That(this._player.Account!.WCoinC, Is.EqualTo(1000));
        Assert.That(this._player.Account.AttributeGrants, Is.Empty);
    }

    /// <summary>Missing offer tokens and unsupported actions cannot mutate an authenticated account.</summary>
    /// <param name="action">The untrusted action.</param>
    /// <returns>The test task.</returns>
    [TestCase("purchase")]
    [TestCase("grant")]
    public async Task InvalidSelectionCannotChargeAsync(string action)
    {
        Assert.That(await this._actions.HandleAsync(this._player, new(action, Guid.NewGuid(), 1, 7)).ConfigureAwait(false), Is.Null);
        Assert.That(this._player.Account!.WCoinC, Is.EqualTo(1000));
    }

    /// <summary>A queued request is refused after the player leaves the world.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LeftWorldCannotPurchaseAsync()
    {
        var request = await this.SelectionAsync().ConfigureAwait(false);
        this._player.Account = null;
        Assert.That(await this._actions.HandleAsync(this._player, request).ConfigureAwait(false), Is.Null);
    }

    /// <summary>Two purchases reviewed from the same expiry cannot both change that expiry.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ConcurrentSelectionsUsePersistenceLockAndOfferVersionAsync()
    {
        var request = await this.SelectionAsync().ConfigureAwait(false);
        var responses = await Task.WhenAll(this._actions.HandleAsync(this._player, request).AsTask(), this._actions.HandleAsync(this._player, request with { OperationId = Guid.NewGuid() }).AsTask()).ConfigureAwait(false);
        var statuses = responses.Select(json => JsonDocument.Parse(json!)).ToArray();
        try
        {
            Assert.That(statuses.Select(json => json.RootElement.GetProperty("status").GetString()), Is.EquivalentTo(new[] { "Applied", "PriceChanged" }));
            Assert.That(this._player.Account!.WCoinC, Is.EqualTo(900));
        }
        finally
        {
            foreach (var document in statuses)
            {
                document.Dispose();
            }
        }
    }

    private async ValueTask<VipGameRequest> SelectionAsync()
    {
        using var state = JsonDocument.Parse((await this._actions.HandleAsync(this._player, new("state")).ConfigureAwait(false))!);
        var offer = state.RootElement.GetProperty("offers")[0];
        return new("purchase", Guid.NewGuid(), 1, 7, offer.GetProperty("offerToken").GetString());
    }
    private sealed class TestClock : TimeProvider
    {
        private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => this._now;
    }

}
