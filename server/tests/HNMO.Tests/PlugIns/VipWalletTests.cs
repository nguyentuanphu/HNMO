// <copyright file="VipWalletTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using System.Threading;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Checks wallet mutations, durable replay proof and the actual account grants used at login.</summary>
[TestFixture]
public class VipWalletTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 1, 0, 0, DateTimeKind.Utc);
    private InMemoryPersistenceContextProvider _provider = null!;
    private IPlayerContext _context = null!;
    private GameConfiguration _game = null!;
    private Account _account = null!;
    private VipConfiguration _configuration = null!;
    private VipWallet _wallet = null!;
    private VipStateStore _store = null!;

    /// <summary>Creates real identified in-memory entities with the canonical supported attributes.</summary>
    [SetUp]
    public void SetUp()
    {
        this._provider = new();
        using var setup = this._provider.CreateNewContext();
        this._game = setup.CreateNew<GameConfiguration>();
        foreach (var attribute in new[] { Stats.IsVip, Stats.BonusExperienceRate, Stats.MoneyAmountRate })
        {
            this._game.Attributes.Add(setup.CreateNew<AttributeDefinition>(attribute.Id, attribute.Designation, attribute.Description));
        }
        this._context = this._provider.CreateNewPlayerContext(this._game);
        this._account = this._context.CreateNew<Account>();
        this._account.LoginName = "ann";
        this._account.WCoinC = 1000;
        this._configuration = new() { Enabled = true };
        this._store = VipStateStore.CreateInMemory();
        this._wallet = new(this._configuration, this._store, this._game, NullLogger.Instance);
    }

    /// <summary>Releases the owned test context.</summary>
    [TearDown]
    public void TearDown() => this._context?.Dispose();

    /// <summary>One debit and exactly three timed grants survive repeated requests and a fresh context.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PurchaseAndReplayShareOneDebitAndExpiryAsync()
    {
        var operation = Guid.NewGuid();
        var first = await this.BuyAsync(operation, Now).ConfigureAwait(false);
        var repeated = await this.BuyAsync(operation, Now.AddMinutes(1)).ConfigureAwait(false);
        Assert.That((first.Status, repeated.Status), Is.EqualTo(("Applied", "Applied")));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(this._account.AttributeGrants.Select(grant => grant.ValidUntil), Is.All.EqualTo(Now.AddDays(7)));
        Assert.That(this._account.GetAccountAttributeValue(Stats.IsVip, Now), Is.EqualTo(1));
        Assert.That(this._account.GetAccountAttributeValue(Stats.BonusExperienceRate, Now), Is.EqualTo(0.05f));
        Assert.That(this._account.GetAccountAttributeValue(Stats.MoneyAmountRate, Now), Is.EqualTo(0.05f));
        using var fresh = this._provider.CreateNewPlayerContext(this._game);
        var account = (await fresh.GetAccountByLoginNameAsync("ann").ConfigureAwait(false))!;
        var recovered = await this._wallet.PurchaseAsync(fresh, account, operation, 1, 7, Now.AddMinutes(2), () => fresh.SaveChangesAsync()).ConfigureAwait(false);
        Assert.That(recovered.Status, Is.EqualTo("Applied"));
        Assert.That(account.WCoinC, Is.EqualTo(900));
        Assert.That(await fresh.GetCashShopCoinHistoryAsync(account.GetId(), 0, 20).ConfigureAwait(false), Has.Count.EqualTo(1));
    }

    /// <summary>A failed save retains one context mutation; retry flushes it instead of repeating the debit.</summary>
    /// <param name="throws">Whether persistence throws rather than returning false.</param>
    /// <returns>The test task.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task FailedSaveRetriesTheSameMutationAsync(bool throws)
    {
        var operation = Guid.NewGuid();
        var failed = await this._wallet.PurchaseAsync(this._context, this._account, operation, 1, 7, Now, () => throws ? ValueTask.FromException<bool>(new InvalidOperationException("injected save failure")) : ValueTask.FromResult(false)).ConfigureAwait(false);
        Assert.That(failed.Status, Is.EqualTo("Pending"));
        Assert.That(await this._wallet.ReadActiveAsync(this._context, this._account, Now).ConfigureAwait(false), Is.Null);
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That((await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false)).Status, Is.EqualTo("Pending"));
        Assert.That((await this.BuyAsync(operation, Now.AddSeconds(1)).ConfigureAwait(false)).Status, Is.EqualTo("Applied"));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false), Is.Empty);
    }

    /// <summary>Renewal replaces only owned benefits and adds days to the original expiry.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ExtensionKeepsForeignGrantsAndDoesNotStackVipAsync()
    {
        var foreign = this._context.CreateNew<AccountAttributeGrant>();
        foreign.Attribute = this._game.Attributes.Single(attribute => attribute.Id == Stats.BonusExperienceRate.Id);
        foreign.Value = 0.2f;
        this._account.AttributeGrants.Add(foreign);
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        var second = await this.BuyAsync(Guid.NewGuid(), Now.AddDays(1)).ConfigureAwait(false);
        Assert.That(second.Membership!.UntilUtc, Is.EqualTo(Now.AddDays(14)));
        Assert.That(this._account.WCoinC, Is.EqualTo(800));
        Assert.That(this._account.GetAccountAttributeValue(Stats.IsVip, Now.AddDays(1)), Is.EqualTo(1));
        Assert.That(this._account.GetAccountAttributeValue(Stats.BonusExperienceRate, Now.AddDays(1)), Is.EqualTo(0.25f));
        Assert.That(foreign.ValidUntil, Is.Null);
        Assert.That(this._account.GetAccountAttributeValue(Stats.IsVip, Now.AddDays(14)), Is.Zero);
        Assert.That(this._account.GetAccountAttributeValue(Stats.BonusExperienceRate, Now.AddDays(14)), Is.EqualTo(0.2f));
    }

    /// <summary>Insufficient funds and a changed selection cannot take coins or mutate unrelated privileges.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RefusesInsufficientFundsAndConflictingReplayAsync()
    {
        this._account.WCoinC = 99;
        var operation = Guid.NewGuid();
        Assert.That((await this.BuyAsync(operation, Now).ConfigureAwait(false)).Rule, Is.EqualTo(VipRuleResult.InsufficientCoins));
        Assert.That(this._account.WCoinC, Is.EqualTo(99));
        Assert.That(this._account.AttributeGrants, Is.Empty);
        this._account.WCoinC = 1000;
        Assert.That((await this.BuyAsync(operation, Now).ConfigureAwait(false)).Status, Is.EqualTo("Applied"));
        var changed = await this._wallet.PurchaseAsync(this._context, this._account, operation, 2, 30, Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false);
        Assert.That(changed.Status, Is.EqualTo("Conflict"));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
    }

    /// <summary>Expired or administratively removed grants cannot be recreated by a paid receipt replay.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PaidReplayNeverResurrectsRemovedOrExpiredGrantsAsync()
    {
        var operation = Guid.NewGuid();
        await this.BuyAsync(operation, Now).ConfigureAwait(false);
        var expired = await this.BuyAsync(operation, Now.AddDays(7)).ConfigureAwait(false);
        Assert.That(expired.Membership, Is.Null);
        this._account.AttributeGrants.Clear();
        var removed = await this.BuyAsync(operation, Now.AddMinutes(2)).ConfigureAwait(false);
        Assert.That(removed.Membership, Is.Null);
        Assert.That(this._account.AttributeGrants, Is.Empty);
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
    }

    /// <summary>A legacy VIP grant owned by another feature is not overwritten or stacked.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RefusesForeignVipOwnershipAsync()
    {
        var existing = this._context.CreateNew<AccountAttributeGrant>();
        existing.Attribute = this._game.Attributes.Single(attribute => attribute.Id == Stats.IsVip.Id);
        existing.Value = 2;
        existing.ValidUntil = Now.AddDays(3);
        this._account.AttributeGrants.Add(existing);
        Assert.That((await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false)).Status, Is.EqualTo("Conflict"));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(existing.ValidUntil, Is.EqualTo(Now.AddDays(3)));
    }

    /// <summary>The actual attribute adapter applies bought bonuses and removes them at the shared expiry.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PurchasedBenefitsApplyAndExpireInTheAttributeSystemAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        var now = Now;
        var clock = new Mock<TimeProvider>();
        clock.Setup(value => value.GetUtcNow()).Returns(() => new DateTimeOffset(now));
        TimerCallback? elapsed = null;
        object? timerState = null;
        clock.Setup(value => value.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
            .Callback<TimerCallback, object?, TimeSpan, TimeSpan>((callback, state, _, _) =>
            {
                elapsed = callback;
                timerState = state;
            }).Returns(Mock.Of<ITimer>());
        using var powerUps = new AccountAttributeGrantPowerUps(clock.Object);
        var attributes = new AttributeSystem([], [], []);
        powerUps.Apply(this._account, attributes);
        Assert.That(attributes[Stats.IsVip], Is.EqualTo(1));
        Assert.That(attributes[Stats.BonusExperienceRate], Is.EqualTo(0.05f));
        Assert.That(attributes[Stats.MoneyAmountRate], Is.EqualTo(0.05f));
        now = Now.AddDays(7);
        Assert.That(elapsed, Is.Not.Null);
        elapsed!(timerState);
        Assert.That(attributes[Stats.IsVip], Is.Zero);
        Assert.That(attributes[Stats.BonusExperienceRate], Is.Zero);
        Assert.That(attributes[Stats.MoneyAmountRate], Is.Zero);
    }

    /// <summary>A failed initial journal write cannot debit coins, grant benefits or call the account save.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task JournalMustBeDurableBeforeAnyAccountMutationAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"hnmo-vip-journal-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, "state");
        try
        {
            Directory.CreateDirectory(root);
            var store = VipStateStore.Open(new HnmoStateStore(Path.Combine(directory, HnmoStateStore.StateFileName)));
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            File.WriteAllText(directory, "Block journal writes with a file instead of a directory.");
            var wallet = new VipWallet(this._configuration, store, this._game, NullLogger.Instance);
            var operation = Guid.NewGuid();
            var saved = false;
            Assert.ThrowsAsync<IOException>(async () => await wallet.PurchaseAsync(this._context, this._account, operation, 1, 7, Now, () =>
            {
                saved = true;
                return this._context.SaveChangesAsync();
            }).ConfigureAwait(false));
            Assert.That(saved, Is.False);
            Assert.That(this._account.WCoinC, Is.EqualTo(1000));
            Assert.That(this._account.AttributeGrants, Is.Empty);
            Assert.That(await this._context.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false), Is.Empty);
            File.Delete(directory);
            var result = await wallet.PurchaseAsync(this._context, this._account, operation, 1, 7, Now.AddSeconds(1), () => this._context.SaveChangesAsync()).ConfigureAwait(false);
            Assert.That(result.Status, Is.EqualTo("Applied"));
            Assert.That(this._account.WCoinC, Is.EqualTo(900));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>Durable account proof recovers after confirmation failure and a disabled or changed host offer.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task CommittedProofRecoversAfterSharedStateWriteFailureAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"hnmo-vip-confirm-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, "state");
        var backup = Path.Combine(root, "moved");
        var path = Path.Combine(directory, HnmoStateStore.StateFileName);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        try
        {
            var store = VipStateStore.Open(new HnmoStateStore(path));
            var wallet = new VipWallet(this._configuration, store, this._game, NullLogger.Instance);
            var operation = Guid.NewGuid();
            var result = await wallet.PurchaseAsync(this._context, this._account, operation, 1, 7, Now, async () =>
            {
                var saved = await this._context.SaveChangesAsync().ConfigureAwait(false);
                Directory.Move(directory, backup);
                File.WriteAllText(directory, "Block the shared-state confirmation after the account commit.");
                return saved;
            }).ConfigureAwait(false);
            Assert.That(result.Status, Is.EqualTo("Pending"));
            Assert.That(this._account.WCoinC, Is.EqualTo(900));
            var persisted = new HnmoStateStore(Path.Combine(backup, HnmoStateStore.StateFileName)).ReadSection<VipState>("vip", options)!;
            Assert.That(persisted.WalletIntents.ContainsKey(operation), Is.True);
            Assert.That(persisted.Payments, Is.Empty);
            File.Delete(directory);
            Directory.Move(backup, directory);
            this._configuration.Enabled = false;
            this._configuration.Levels[0].Offers[0] = new(7, 999, CashShopCoinType.WCoinP);
            var restarted = new VipWallet(this._configuration, VipStateStore.Open(new HnmoStateStore(path)), this._game, NullLogger.Instance);
            using var fresh = this._provider.CreateNewPlayerContext(this._game);
            var account = (await fresh.GetAccountByLoginNameAsync("ann").ConfigureAwait(false))!;
            var recovered = await restarted.PurchaseAsync(fresh, account, operation, 1, 7, Now.AddMinutes(1), () => throw new InvalidOperationException("A committed proof must not debit or save the account again.")).ConfigureAwait(false);
            Assert.That(recovered.Status, Is.EqualTo("Applied"));
            Assert.That(recovered.Membership!.UntilUtc, Is.EqualTo(Now.AddDays(7)));
            Assert.That(account.WCoinC, Is.EqualTo(900));
            Assert.That(account.AttributeGrants, Has.Count.EqualTo(3));
            Assert.That(new HnmoStateStore(path).ReadSection<VipState>("vip", options)!.Payments.ContainsKey(operation), Is.True);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>Unexpected changes to the owned grant require review instead of overwriting the authoritative entitlement.</summary>
    /// <param name="changeExpiry">Whether the expiry rather than the level was changed externally.</param>
    /// <returns>The test task.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task RefusesDriftInTheAuthoritativeGrantAsync(bool changeExpiry)
    {
        var original = Guid.NewGuid();
        await this.BuyAsync(original, Now).ConfigureAwait(false);
        var grant = this._account.AttributeGrants.Single(value => value.Attribute?.Id == Stats.IsVip.Id);
        if (changeExpiry)
        {
            grant.ValidUntil = Now.AddDays(9);
        }
        else
        {
            grant.Value = 2;
        }

        Assert.That((await this.BuyAsync(Guid.NewGuid(), Now.AddMinutes(1)).ConfigureAwait(false)).Status, Is.EqualTo("Conflict"));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        var replay = await this.BuyAsync(original, Now.AddMinutes(2)).ConfigureAwait(false);
        Assert.That(replay.Membership, Is.Null);
    }

    /// <summary>A changed displayed price or benefit requires fresh review before taking any coins.</summary>
    /// <param name="change">The reviewed term changed by the host.</param>
    /// <returns>The test task.</returns>
    [TestCase("price")]
    [TestCase("experience")]
    [TestCase("daily")]
    public async Task ChangedReviewCannotChargeUntilReviewedAgainAsync(string change)
    {
        var level = this._configuration.Levels[0];
        var token = VipOfferToken.Create(this._account.GetId(), level, level.Offers[0], null);
        switch (change)
        {
            case "price": level.Offers[0] = new(7, 120, CashShopCoinType.WCoinC); break;
            case "experience": level.ExperiencePercent = 10; break;
            case "daily": level.DailyCoins = 20; break;
        }

        var operation = Guid.NewGuid();
        var changed = await this._wallet.PurchaseAsync(this._context, this._account, operation, 1, 7, Now, () => this._context.SaveChangesAsync(), token).ConfigureAwait(false);
        Assert.That(changed.Status, Is.EqualTo("PriceChanged"));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(this._account.AttributeGrants, Is.Empty);
        Assert.That(await this._context.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false), Is.Empty);
        var reviewed = VipOfferToken.Create(this._account.GetId(), level, level.Offers[0], null);
        var accepted = await this._wallet.PurchaseAsync(this._context, this._account, operation, 1, 7, Now, () => this._context.SaveChangesAsync(), reviewed.ToLowerInvariant()).ConfigureAwait(false);
        Assert.That(accepted.Status, Is.EqualTo("Applied"));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000 - level.Offers[0].Price));
    }

    /// <summary>An old tab cannot buy against a changed expiry, but a paid retry still uses its original version.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReviewVersionTracksTheSubscriptionAndPaidRetryAsync()
    {
        var level = this._configuration.Levels[0];
        var firstToken = VipOfferToken.Create(this._account.GetId(), level, level.Offers[0], null);
        var first = Guid.NewGuid();
        await this._wallet.PurchaseAsync(this._context, this._account, first, 1, 7, Now, () => this._context.SaveChangesAsync(), firstToken).ConfigureAwait(false);
        var second = Guid.NewGuid();
        var stale = await this._wallet.PurchaseAsync(this._context, this._account, second, 1, 7, Now.AddMinutes(1), () => this._context.SaveChangesAsync(), firstToken).ConfigureAwait(false);
        Assert.That(stale.Status, Is.EqualTo("PriceChanged"));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        var nextToken = VipOfferToken.Create(this._account.GetId(), level, level.Offers[0], Now.AddDays(7));
        var renewed = await this._wallet.PurchaseAsync(this._context, this._account, second, 1, 7, Now.AddMinutes(1), () => this._context.SaveChangesAsync(), nextToken).ConfigureAwait(false);
        Assert.That(renewed.Membership!.UntilUtc, Is.EqualTo(Now.AddDays(14)));
        var replay = await this._wallet.PurchaseAsync(this._context, this._account, first, 1, 7, Now.AddMinutes(2), () => this._context.SaveChangesAsync(), firstToken).ConfigureAwait(false);
        Assert.That(replay.Membership, Is.Null);
        Assert.That(this._account.WCoinC, Is.EqualTo(800));
        Assert.That(this._account.GetAccountAttributeValue(Stats.IsVip, Now.AddMinutes(2)), Is.EqualTo(1));
    }

    /// <summary>A different host or an older file projection rebuilds ownership from database proof before renewal.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task RebuildsMissingProjectionFromOwnedDatabaseProofAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        using var fresh = this._provider.CreateNewPlayerContext(this._game);
        var account = (await fresh.GetAccountByLoginNameAsync("ann").ConfigureAwait(false))!;
        var other = new VipWallet(this._configuration, VipStateStore.CreateInMemory(), this._game, NullLogger.Instance);
        var active = await other.ReadActiveAsync(fresh, account, Now.AddHours(1)).ConfigureAwait(false);
        Assert.That(active!.Intent.Membership.UntilUtc, Is.EqualTo(Now.AddDays(7)));
        var token = VipOfferToken.Create(account.GetId(), this._configuration.Levels[0], this._configuration.Levels[0].Offers[0], Now.AddDays(7));
        var renewed = await other.PurchaseAsync(fresh, account, Guid.NewGuid(), 1, 7, Now.AddHours(1), () => fresh.SaveChangesAsync(), token).ConfigureAwait(false);
        Assert.That(renewed.Status, Is.EqualTo("Applied"));
        Assert.That(renewed.Membership!.UntilUtc, Is.EqualTo(Now.AddDays(14)));
        Assert.That(account.WCoinC, Is.EqualTo(800));
        Assert.That(account.GetAccountAttributeValue(Stats.IsVip, Now.AddHours(1)), Is.EqualTo(1));
    }

    /// <summary>Cold reconstruction pages past older history without treating unrelated grants as proof.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReconstructionReadsBeyondOneThousandReceiptsAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        for (var index = 0; index < 1100; index++)
        {
            var row = this._context.CreateNew<CashShopCoinGrant>();
            row.AccountId = this._account.GetId();
            row.CreatedAt = Now.AddMinutes(1).AddSeconds(index);
            row.AppliedAt = row.CreatedAt;
            row.Reason = "Retained fixture receipt";
        }

        await this._context.SaveChangesAsync().ConfigureAwait(false);
        var other = new VipWallet(this._configuration, VipStateStore.CreateInMemory(), this._game, NullLogger.Instance);
        var proof = await other.ReadActiveAsync(this._context, this._account, Now.AddDays(1)).ConfigureAwait(false);
        Assert.That(proof, Is.Not.Null);
        Assert.That(proof!.Intent.Membership.UntilUtc, Is.EqualTo(Now.AddDays(7)));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
    }

    /// <summary>Neither a saved membership nor a paid receipt can invent removed account grants.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ReadingPaidStatusCannotRecreateRemovedEntitlementsAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        this._account.AttributeGrants.Clear();
        var proof = await this._wallet.ReadActiveAsync(this._context, this._account, Now.AddHours(1)).ConfigureAwait(false);
        Assert.That(proof, Is.Null);
        Assert.That(this._account.AttributeGrants, Is.Empty);
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        Assert.That((await this.BuyAsync(Guid.NewGuid(), Now.AddHours(1)).ConfigureAwait(false)).Status, Is.EqualTo("Conflict"));
    }

    /// <summary>Daily credits use their purchased terms and cannot be collected twice after renewal or from another host.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DailyBonusIsOnePendingCreditPerAccountDayAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        this._configuration.Levels[0].DailyCoins = 999;
        var rewards = new VipDailyCoins(this._provider, this._game, this._wallet, this._store, NullLogger.Instance);
        var first = await rewards.ClaimAsync(this._context, this._account, Now).ConfigureAwait(false);
        var second = await rewards.ClaimAsync(this._context, this._account, Now.AddMinutes(1)).ConfigureAwait(false);
        Assert.That((first.Status, first.Amount, second.Status), Is.EqualTo(("DailyApplied", 10, "AlreadyClaimed")));
        var pending = await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false);
        Assert.That(pending.Single().Amount, Is.EqualTo(10));
        Assert.That(pending.Single().CoinType, Is.EqualTo(CashShopCoinType.GoblinPoints));
        Assert.That(pending.Single().AppliedAt, Is.Null);
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        Assert.That(this._account.GoblinPoints, Is.Zero);
        await this.BuyAsync(Guid.NewGuid(), Now.AddMinutes(2)).ConfigureAwait(false);
        var other = new VipDailyCoins(this._provider, this._game, this._wallet, VipStateStore.CreateInMemory(), NullLogger.Instance);
        Assert.That((await other.ClaimAsync(this._context, this._account, Now.AddMinutes(3)).ConfigureAwait(false)).Status, Is.EqualTo("AlreadyClaimed"));
        Assert.That(await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false), Has.Count.EqualTo(1));
    }

    /// <summary>Day boundaries follow Sydney midnight and exact expiry cannot authorize another reward.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DailyBonusUsesSydneyMidnightAndRealExpiryAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        var rewards = new VipDailyCoins(this._provider, this._game, this._wallet, this._store, NullLogger.Instance);
        var beforeMidnight = Now.Date.AddHours(12).AddMinutes(59);
        Assert.That((await rewards.ClaimAsync(this._context, this._account, beforeMidnight).ConfigureAwait(false)).Status, Is.EqualTo("DailyApplied"));
        Assert.That((await rewards.ClaimAsync(this._context, this._account, beforeMidnight.AddMinutes(2)).ConfigureAwait(false)).Status, Is.EqualTo("DailyApplied"));
        var rows = await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false);
        Assert.That(rows.Select(row => VipDailyCoins.ReadReceipt(row.Reason)!.SydneyDay), Is.EquivalentTo(new[] { "2026-11-17", "2026-11-18" }));
        Assert.That((await rewards.ClaimAsync(this._context, this._account, Now.AddDays(7)).ConfigureAwait(false)).Status, Is.EqualTo("DailyUnavailable"));
        Assert.That(await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false), Has.Count.EqualTo(2));
    }

    /// <summary>A full coin balance leaves the daily reward available rather than discarding it.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DailyBonusWaitsForCoinCapacityAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        this._account.GoblinPoints = int.MaxValue;
        var rewards = new VipDailyCoins(this._provider, this._game, this._wallet, this._store, NullLogger.Instance);
        Assert.That((await rewards.ClaimAsync(this._context, this._account, Now).ConfigureAwait(false)).Status, Is.EqualTo("BalanceFull"));
        Assert.That(await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false), Is.Empty);
        this._account.GoblinPoints -= 10;
        Assert.That((await rewards.ClaimAsync(this._context, this._account, Now).ConfigureAwait(false)).Status, Is.EqualTo("DailyApplied"));
        Assert.That((await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false)).Single().Amount, Is.EqualTo(10));
    }

    /// <summary>Unconfirmed credit writes do not consume the day and a later healthy request records it once.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task FailedDailyCreditRemainsRetryableAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        var failedContext = new Mock<IPlayerContext>();
        failedContext.Setup(context => context.GetAccountIdByLoginNameAsync("ann", It.IsAny<CancellationToken>())).ReturnsAsync(this._account.GetId());
        failedContext.Setup(context => context.CreateNew<CashShopCoinGrant>(It.IsAny<object?[]>())).Returns(new HNMO.Persistence.BasicModel.CashShopCoinGrant());
        failedContext.Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var failedProvider = new Mock<IPersistenceContextProvider>();
        failedProvider.Setup(provider => provider.CreateNewPlayerContext(this._game)).Returns(failedContext.Object);
        var failed = new VipDailyCoins(failedProvider.Object, this._game, this._wallet, this._store, NullLogger.Instance);
        Assert.That((await failed.ClaimAsync(this._context, this._account, Now).ConfigureAwait(false)).Status, Is.EqualTo("DailyPending"));
        Assert.That(await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false), Is.Empty);
        var healthy = new VipDailyCoins(this._provider, this._game, this._wallet, this._store, NullLogger.Instance);
        Assert.That((await healthy.ClaimAsync(this._context, this._account, Now).ConfigureAwait(false)).Status, Is.EqualTo("DailyApplied"));
        Assert.That(await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false), Has.Count.EqualTo(1));
    }

    /// <summary>Separate requests serialize per account and the database reference remains one reward.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ConcurrentDailyRequestsKeepOneCreditAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        using var fresh = this._provider.CreateNewPlayerContext(this._game);
        var account = (await fresh.GetAccountByLoginNameAsync("ann").ConfigureAwait(false))!;
        var rewards = new VipDailyCoins(this._provider, this._game, this._wallet, this._store, NullLogger.Instance);
        var other = new VipDailyCoins(this._provider, this._game, this._wallet, VipStateStore.CreateInMemory(), NullLogger.Instance);
        var results = await Task.WhenAll(rewards.ClaimAsync(this._context, this._account, Now).AsTask(), other.ClaimAsync(fresh, account, Now).AsTask()).ConfigureAwait(false);
        Assert.That(results.Select(result => result.Status), Is.EquivalentTo(new[] { "DailyApplied", "AlreadyClaimed" }));
        Assert.That(await this._context.GetPendingCashShopCoinGrantsAsync(this._account.GetId()).ConfigureAwait(false), Has.Count.EqualTo(1));
    }

    /// <summary>A database query returning the same marked entity after a failed save cannot replay its credit or debit.</summary>
    /// <param name="amount">The pending credit or debit.</param>
    /// <returns>The test task.</returns>
    [TestCase(20)]
    [TestCase(-20)]
    public async Task PendingCoinRetryDoesNotReplayTrackedChangesAsync(int amount)
    {
        var grant = this._context.CreateNew<CashShopCoinGrant>();
        grant.AccountId = this._account.GetId();
        grant.CoinType = CashShopCoinType.WCoinC;
        grant.Amount = amount;
        var query = new Mock<IPlayerContext>();
        query.Setup(context => context.GetPendingCashShopCoinGrantsAsync(this._account.GetId(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { grant });
        Assert.That(await VipPendingCoins.ApplyAsync(query.Object, this._account, Now, () => ValueTask.FromResult(false)).ConfigureAwait(false), Is.False);
        Assert.That(this._account.WCoinC, Is.EqualTo(1000 + amount));
        Assert.That(grant.AppliedAt, Is.EqualTo(Now));
        Assert.That(await VipPendingCoins.ApplyAsync(query.Object, this._account, Now.AddSeconds(1), () => ValueTask.FromResult(true)).ConfigureAwait(false), Is.True);
        Assert.That(this._account.WCoinC, Is.EqualTo(1000 + amount));
        Assert.That(grant.AppliedAt, Is.EqualTo(Now));
    }

    /// <summary>Applying a row from another account cannot change a balance or consume its marker.</summary>
    [Test]
    public void PendingCoinApplicationKeepsAccountOwnership()
    {
        var grant = this._context.CreateNew<CashShopCoinGrant>();
        grant.AccountId = Guid.NewGuid();
        grant.Amount = 20;
        Assert.That(this._account.TryApplyCashShopCoinGrant(grant, Now), Is.False);
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(grant.AppliedAt, Is.Null);
    }

    /// <summary>Host grants share timed proof without spending coins and cannot be replayed as player purchases.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task AdministrativeGrantIsFreeAndBoundToAttributionAsync()
    {
        this._configuration.Enabled = false;
        var admin = new VipAdministration(this._configuration, this._store, this._game, NullLogger.Instance);
        var operation = Guid.NewGuid();
        var actor = new VipAdministrativeGrant("host", "Compensation for the outage");
        var first = await admin.GrantAsync(this._context, this._account, operation, 1, 14, actor, Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false);
        var retry = await admin.GrantAsync(this._context, this._account, operation, 1, 14, actor, Now.AddMinutes(1), () => this._context.SaveChangesAsync()).ConfigureAwait(false);
        Assert.That((first.Status, retry.Status), Is.EqualTo(("Applied", "Applied")));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(this._account.AttributeGrants.Select(grant => grant.ValidUntil), Is.All.EqualTo(Now.AddDays(14)));
        var row = (await this._context.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false)).Single();
        Assert.That(row.Amount, Is.Zero);
        Assert.That(row.AppliedAt, Is.Not.Null);
        Assert.That(VipWallet.ReadReceipt(row.Reason)!.Intent.Administration, Is.EqualTo(actor));
        Assert.That(this._store.Data.Audit.Single().Action, Is.EqualTo("grant"));
        Assert.That(this._store.Data.Audit.Single().Reason, Is.EqualTo(actor.Reason));
        Assert.That((await this._wallet.PurchaseAsync(this._context, this._account, operation, 1, 14, Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("Conflict"));
        Assert.That((await admin.GrantAsync(this._context, this._account, operation, 1, 14, actor with { Actor = "another host" }, Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("Conflict"));
    }

    /// <summary>A failed host grant save retries the same grants; invalid attribution never creates an intent.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task AdministrativeGrantRetryAndRequiredReasonAsync()
    {
        var admin = new VipAdministration(this._configuration, this._store, this._game, NullLogger.Instance);
        var operation = Guid.NewGuid();
        var invalid = await admin.GrantAsync(this._context, this._account, operation, 1, 7, new("host", " "), Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false);
        Assert.That(invalid.Status, Is.EqualTo("Refused"));
        Assert.That(this._store.Data.WalletIntents, Is.Empty);
        var actor = new VipAdministrativeGrant("host", "Welcome reward");
        var pending = await admin.GrantAsync(this._context, this._account, operation, 1, 7, actor, Now, () => ValueTask.FromResult(false)).ConfigureAwait(false);
        Assert.That(pending.Status, Is.EqualTo("Pending"));
        var publicState = await VipStateWriter.WriteAsync(this._configuration, this._store, this._wallet, this._context, this._account, Now).ConfigureAwait(false);
        using var publicJson = JsonDocument.Parse(publicState);
        Assert.That(publicJson.RootElement.GetProperty("pending").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(publicState, Does.Not.Contain(actor.Reason));
        Assert.That((await admin.GrantAsync(this._context, this._account, operation, 1, 7, actor, Now.AddMinutes(1), () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("Applied"));
        Assert.That(this._account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
    }

    /// <summary>Removal affects only owned grants and an old removal replay cannot erase a newer subscription.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task AdministrativeRemovalKeepsForeignBonusesAndNewPurchasesAsync()
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        var foreign = this._context.CreateNew<AccountAttributeGrant>();
        foreign.Attribute = this._game.Attributes.Single(value => value.Id == Stats.BonusExperienceRate.Id);
        foreign.Value = 0.1f;
        foreign.ValidFrom = Now;
        foreign.ValidUntil = Now.AddDays(30);
        this._account.AttributeGrants.Add(foreign);
        var admin = new VipAdministration(this._configuration, this._store, this._game, NullLogger.Instance);
        var operation = Guid.NewGuid();
        var actor = new VipAdministrativeGrant("host", "Revoke this interval");
        var result = await admin.RemoveAsync(this._context, this._account, operation, actor, Now.AddHours(1), () => this._context.SaveChangesAsync()).ConfigureAwait(false);
        Assert.That(result.Status, Is.EqualTo("Applied"));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        Assert.That(this._account.GetAccountAttributeValue(Stats.IsVip, Now.AddHours(1)), Is.Zero);
        Assert.That(this._account.GetAccountAttributeValue(Stats.BonusExperienceRate, Now.AddHours(1)), Is.EqualTo(0.1f));
        var row = (await this._context.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false)).Single(value => value.Reference.StartsWith("vipremove:", StringComparison.Ordinal));
        Assert.That(row.Amount, Is.Zero);
        Assert.That(VipWallet.ReadRemovalReceipt(row.Reason)!.Intent.Attribution, Is.EqualTo(actor));
        Assert.That(this._store.Data.Memberships, Is.Empty);
        var fresh = await this.BuyAsync(Guid.NewGuid(), Now.AddHours(2)).ConfigureAwait(false);
        Assert.That(fresh.Status, Is.EqualTo("Applied"));
        var expiry = fresh.Membership!.UntilUtc;
        Assert.That((await admin.RemoveAsync(this._context, this._account, operation, actor, Now.AddHours(3), () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("Applied"));
        Assert.That((await this._wallet.ReadActiveAsync(this._context, this._account, Now.AddHours(3)).ConfigureAwait(false))!.Intent.Membership.UntilUtc, Is.EqualTo(expiry));
        Assert.That(this._account.WCoinC, Is.EqualTo(800));
        Assert.That(this._store.Data.Audit.Count(value => value.Action == "remove"), Is.EqualTo(1));
    }

    /// <summary>Failed removal saves retain one attributed marker, block other mutations, and retry without a second change.</summary>
    /// <param name="throws">Whether persistence throws rather than returning false.</param>
    /// <returns>The test task.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task AdministrativeRemovalSaveFailureRecoversAsync(bool throws)
    {
        await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false);
        var admin = new VipAdministration(this._configuration, this._store, this._game, NullLogger.Instance);
        var operation = Guid.NewGuid();
        var actor = new VipAdministrativeGrant("host", "Correct an earlier grant");
        var pending = await admin.RemoveAsync(this._context, this._account, operation, actor, Now.AddHours(1), () => throws ? ValueTask.FromException<bool>(new InvalidOperationException("injected removal failure")) : ValueTask.FromResult(false)).ConfigureAwait(false);
        Assert.That(pending.Status, Is.EqualTo("Pending"));
        Assert.That((await this.BuyAsync(Guid.NewGuid(), Now.AddHours(1)).ConfigureAwait(false)).Status, Is.EqualTo("Pending"));
        Assert.That((await admin.RemoveAsync(this._context, this._account, operation, actor with { Reason = "changed reason" }, Now.AddHours(1), () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("Conflict"));
        Assert.That((await admin.RemoveAsync(this._context, this._account, operation, actor, Now.AddHours(2), () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("Applied"));
        Assert.That(this._account.WCoinC, Is.EqualTo(900));
        Assert.That(this._account.GetAccountAttributeValue(Stats.IsVip, Now.AddHours(2)), Is.Zero);
        var rows = await this._context.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false);
        Assert.That(rows.Count(value => value.Reference.StartsWith("vipremove:", StringComparison.Ordinal)), Is.EqualTo(1));
    }

    /// <summary>Unowned legacy VIP and missing reasons cannot be removed by this plug-in.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task AdministrativeRemovalRequiresOwnershipAndReasonAsync()
    {
        var foreign = this._context.CreateNew<AccountAttributeGrant>();
        foreign.Attribute = this._game.Attributes.Single(value => value.Id == Stats.IsVip.Id);
        foreign.Value = 5;
        foreign.ValidFrom = Now;
        foreign.ValidUntil = Now.AddDays(30);
        this._account.AttributeGrants.Add(foreign);
        var admin = new VipAdministration(this._configuration, this._store, this._game, NullLogger.Instance);
        Assert.That((await admin.RemoveAsync(this._context, this._account, Guid.NewGuid(), new("host", " "), Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("Refused"));
        Assert.That((await admin.RemoveAsync(this._context, this._account, Guid.NewGuid(), new("host", "Review legacy entitlement"), Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("Conflict"));
        Assert.That(foreign.ValidUntil, Is.EqualTo(Now.AddDays(30)));
        Assert.That(this._store.Data.RemovalIntents, Is.Empty);
    }

    private ValueTask<VipWalletResult> BuyAsync(Guid operation, DateTime now)
        => this._wallet.PurchaseAsync(this._context, this._account, operation, 1, 7, now, () => this._context.SaveChangesAsync());
}
