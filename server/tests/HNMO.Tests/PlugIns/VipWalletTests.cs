// <copyright file="VipWalletTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;

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
        this._game.Attributes.Add(Stats.IsVip);
        this._game.Attributes.Add(Stats.BonusExperienceRate);
        this._game.Attributes.Add(Stats.MoneyAmountRate);
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
    public void TearDown() => this._context.Dispose();

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
        foreign.Attribute = Stats.BonusExperienceRate;
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
        existing.Attribute = Stats.IsVip;
        existing.Value = 2;
        existing.ValidUntil = Now.AddDays(3);
        this._account.AttributeGrants.Add(existing);
        Assert.That((await this.BuyAsync(Guid.NewGuid(), Now).ConfigureAwait(false)).Status, Is.EqualTo("Conflict"));
        Assert.That(this._account.WCoinC, Is.EqualTo(1000));
        Assert.That(existing.ValidUntil, Is.EqualTo(Now.AddDays(3)));
    }

    private ValueTask<VipWalletResult> BuyAsync(Guid operation, DateTime now)
        => this._wallet.PurchaseAsync(this._context, this._account, operation, 1, 7, now, () => this._context.SaveChangesAsync());
}
