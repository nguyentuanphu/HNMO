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

    private ValueTask<VipWalletResult> BuyAsync(Guid operation, DateTime now)
        => this._wallet.PurchaseAsync(this._context, this._account, operation, 1, 7, now, () => this._context.SaveChangesAsync());
}
