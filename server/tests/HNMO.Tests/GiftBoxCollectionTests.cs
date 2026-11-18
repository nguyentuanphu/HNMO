// <copyright file="GiftBoxCollectionTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Actual goods and balance mutation, replay proof, full-bag refusal and uncertain-save recovery.</summary>
[TestFixture]
public class GiftBoxCollectionTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);
    private InMemoryPersistenceContextProvider _provider = null!;
    private IPlayerContext _context = null!;
    private GameConfiguration _game = null!;
    private ItemDefinition _definition = null!;
    private Account _account = null!;
    private Character _character = null!;
    private GiftBoxDispatch _box = null!;
    private GiftBoxCollection _collection = null!;

    /// <summary>Creates real identified account/inventory entities and an isolated owed mailbox.</summary>
    [SetUp]
    public void SetUp()
    {
        this._provider = new();
        using var setup = this._provider.CreateNewContext();
        this._game = setup.CreateNew<GameConfiguration>();
        this._game.MaximumInventoryMoney = 2000000000;
        this._definition = setup.CreateNew<ItemDefinition>();
        this._definition.Group = 0;
        this._definition.Number = 0;
        this._definition.Width = 1;
        this._definition.Height = 1;
        this._definition.MaximumItemLevel = 15;
        this._definition.Durability = 100;
        this._game.Items.Add(this._definition);
        this._context = this._provider.CreateNewPlayerContext(this._game);
        this._account = this._context.CreateNew<Account>();
        this._account.LoginName = "Ann";
        this._account.WCoinC = 100;
        this._character = this._context.CreateNew<Character>();
        this._character.Name = "AnnHero";
        this._character.Inventory = this._context.CreateNew<ItemStorage>();
        this._character.Inventory.Money = 1000;
        this._account.Characters.Add(this._character);
        this._box = new(new InMemoryHnmoPlayerStateRowBackend());
        this._collection = new(this._box, this._game, NullLogger.Instance);
    }

    /// <summary>Disposes the owned test context.</summary>
    [TearDown]
    public void TearDown() => this._context.Dispose();

    /// <summary>One save contains the real goods, Zen, coins and receipt; fresh-context replay gives nothing more.</summary>
    [Test]
    public async Task DeliveryAndReplayShareOneReceiptAsync()
    {
        var letter = this.Send();
        var first = await this.CollectAsync(letter).ConfigureAwait(false);
        Assert.That(first.Status, Is.EqualTo("Applied"));
        var ids = first.Receipt!.ItemIds.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(this._account.WCoinC, Is.EqualTo(150));
            Assert.That(this._character.Inventory!.Money, Is.EqualTo(2000));
            Assert.That(this._character.Inventory.Items, Has.Count.EqualTo(2));
            Assert.That(this._character.Inventory.Items.Select(item => item.GetId()), Is.EquivalentTo(ids));
            Assert.That(this._character.Inventory.Items.Select(item => item.ItemSlot), Is.EqualTo(new byte[] { 12, 13 }));
        });
        using var restarted = this._provider.CreateNewPlayerContext(this._game);
        var account = (await restarted.GetAccountByLoginNameAsync("Ann").ConfigureAwait(false))!;
        var repeated = await this._collection.CollectAsync(restarted, account, letter, this._character.GetId(), Now.AddDays(5), () => restarted.SaveChangesAsync()).ConfigureAwait(false);
        Assert.That(repeated.Status, Is.EqualTo("AlreadyCollected"));
        Assert.That(repeated.Receipt!.ItemIds, Is.EquivalentTo(ids));
        Assert.That(account.WCoinC, Is.EqualTo(150));
        Assert.That(account.Characters.Single(character => character.GetId() == this._character.GetId()).Inventory!.Items.Select(item => item.ItemSlot), Is.EqualTo(new byte[] { 12, 13 }));
        Assert.That((await restarted.GetCashShopCoinHistoryAsync(account.GetId(), 0, 20).ConfigureAwait(false)), Has.Count.EqualTo(1));
    }

    /// <summary>False or thrown saves retain one staged mutation; retries do not add goods or coins again.</summary>
    /// <param name="throws">Whether the uncertain save throws.</param>
    [TestCase(false)]
    [TestCase(true)]
    public async Task PendingSaveRecoversWithoutDuplicateDeliveryAsync(bool throws)
    {
        var letter = this.Send();
        ValueTask<bool> Fail() => throws ? ValueTask.FromException<bool>(new IOException("injected save failure")) : ValueTask.FromResult(false);
        var first = await this._collection.CollectAsync(this._context, this._account, letter, this._character.GetId(), Now, Fail).ConfigureAwait(false);
        var repeated = await this._collection.CollectAsync(this._context, this._account, letter, this._character.GetId(), Now.AddMinutes(1), Fail).ConfigureAwait(false);
        Assert.That((first.Status, repeated.Status), Is.EqualTo(("Pending", "Pending")));
        Assert.That(this._account.WCoinC, Is.EqualTo(150));
        Assert.That(this._character.Inventory!.Items, Has.Count.EqualTo(2));
        Assert.That((await this.CollectAsync(this.Send()).ConfigureAwait(false)).Status, Is.EqualTo("Pending"));
        Assert.That((await this.CollectAsync(letter, Now.AddDays(2)).ConfigureAwait(false)).Status, Is.EqualTo("Applied"));
        Assert.That(this._account.WCoinC, Is.EqualTo(150));
        Assert.That(this._character.Inventory.Items, Has.Count.EqualTo(2));
        Assert.That((await this._context.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false)), Has.Count.EqualTo(1));
    }

    /// <summary>A pending claim cannot change character; a confirmed replay reports its original character.</summary>
    [Test]
    public async Task TheOriginalTargetIsBoundAcrossRetryAsync()
    {
        var other = this._context.CreateNew<Character>();
        other.Name = "AnnOther";
        other.Inventory = this._context.CreateNew<ItemStorage>();
        this._account.Characters.Add(other);
        var letter = this.Send();
        await this._collection.CollectAsync(this._context, this._account, letter, this._character.GetId(), Now, () => ValueTask.FromResult(false)).ConfigureAwait(false);
        Assert.That((await this._collection.CollectAsync(this._context, this._account, letter, other.GetId(), Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("Conflict"));
        Assert.That((await this.CollectAsync(letter).ConfigureAwait(false)).Status, Is.EqualTo("Applied"));
        var replay = await this._collection.CollectAsync(this._context, this._account, letter, other.GetId(), Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false);
        Assert.That(replay.Status, Is.EqualTo("AlreadyCollected"));
        Assert.That(replay.Receipt!.CharacterId, Is.EqualTo(this._character.GetId()));
        Assert.That(other.Inventory.Items, Is.Empty);
    }

    /// <summary>A partially fitting reward remains entirely owed with no receipt or balance change.</summary>
    [Test]
    public async Task FullBagLeavesAllRewardsOwedAsync()
    {
        for (byte slot = 12; slot < 75; slot++)
        {
            var item = this._context.CreateNew<Item>();
            item.Definition = this._definition;
            item.ItemSlot = slot;
            this._character.Inventory!.Items.Add(item);
        }

        var letter = this.Send();
        Assert.That((await this.CollectAsync(letter).ConfigureAwait(false)).Status, Is.EqualTo("BagFull"));
        Assert.That(this._character.Inventory!.Items, Has.Count.EqualTo(63));
        Assert.That(this._account.WCoinC, Is.EqualTo(100));
        Assert.That(this._character.Inventory.Money, Is.EqualTo(1000));
        Assert.That(this._box.Read(this._account.GetId()), Has.Count.EqualTo(1));
        Assert.That((await this._context.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false)), Is.Empty);
    }

    /// <summary>Insufficient balance capacity refuses the whole claim instead of clipping a promised reward.</summary>
    [Test]
    public async Task FullBalancesDoNotClipOrConsumeMailAsync()
    {
        var letter = this.Send();
        this._account.WCoinC = int.MaxValue - 10;
        Assert.That((await this.CollectAsync(letter).ConfigureAwait(false)).Status, Is.EqualTo("BalanceFull"));
        this._account.WCoinC = 100;
        this._character.Inventory!.Money = this._game.MaximumInventoryMoney - 500;
        Assert.That((await this.CollectAsync(letter).ConfigureAwait(false)).Status, Is.EqualTo("BalanceFull"));
        Assert.That(this._character.Inventory.Items, Is.Empty);
        Assert.That(this._account.WCoinC, Is.EqualTo(100));
        Assert.That((await this._context.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false)), Is.Empty);
    }

    /// <summary>Expired mail, another account and a foreign character cannot mutate a recipient.</summary>
    [Test]
    public async Task OwnershipAndDeadlineAreAuthoritativeAsync()
    {
        var letter = this.Send();
        Assert.That((await this.CollectAsync(letter, Now.AddHours(1)).ConfigureAwait(false)).Status, Is.EqualTo("Expired"));
        var stranger = this._context.CreateNew<Account>();
        stranger.LoginName = "Bob";
        var character = this._context.CreateNew<Character>();
        character.Name = "BobHero";
        character.Inventory = this._context.CreateNew<ItemStorage>();
        stranger.Characters.Add(character);
        Assert.That((await this._collection.CollectAsync(this._context, stranger, letter, character.GetId(), Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("NotFound"));
        Assert.That((await this._collection.CollectAsync(this._context, this._account, letter, character.GetId(), Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false)).Status, Is.EqualTo("InvalidCharacter"));
        Assert.That(this._character.Inventory!.Items, Is.Empty);
    }

    /// <summary>Missing item definitions cannot silently turn a promised item reward into coins only.</summary>
    [Test]
    public async Task InvalidItemRefusesEverythingAsync()
    {
        var letter = this.Send(new() { Coins = 50, Items = [new() { Number = 511 }] });
        Assert.That((await this.CollectAsync(letter).ConfigureAwait(false)).Status, Is.EqualTo("InvalidReward"));
        Assert.That(this._account.WCoinC, Is.EqualTo(100));
        Assert.That(this._character.Inventory!.Items, Is.Empty);
    }

    /// <summary>Concurrent retries of the same mail serialize to a single set of goods and credit.</summary>
    [Test]
    public async Task ConcurrentRetriesDoNotDuplicateRewardsAsync()
    {
        var letter = this.Send();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => this.CollectAsync(letter).AsTask())).ConfigureAwait(false);
        Assert.That(results.Count(result => result.Status == "Applied"), Is.EqualTo(1));
        Assert.That(results.Count(result => result.Status == "AlreadyCollected"), Is.EqualTo(7));
        Assert.That(this._account.WCoinC, Is.EqualTo(150));
        Assert.That(this._character.Inventory!.Items, Has.Count.EqualTo(2));
    }

    /// <summary>Preparation failure detaches new roots before any later account save can commit an unproved gift.</summary>
    [Test]
    public async Task ItemCreationFailureLeavesNoOrphanOrReceiptAsync()
    {
        var letter = this.Send();
        var query = new Mock<IPlayerContext>();
        query.Setup(context => context.GetCashShopCoinGrantByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CashShopCoinGrant?)null);
        query.Setup(context => context.CreateNew<CashShopCoinGrant>(It.IsAny<object?[]>())).Returns(() => this._context.CreateNew<CashShopCoinGrant>());
        var made = 0;
        query.Setup(context => context.CreateNew<Item>(It.IsAny<object?[]>())).Returns(() =>
        {
            if (++made == 2)
            {
                throw new InvalidOperationException("injected item preparation failure");
            }

            return this._context.CreateNew<Item>();
        });
        query.Setup(context => context.Detach(It.IsAny<object>())).Returns((object item) => this._context.Detach(item));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await this._collection.CollectAsync(query.Object, this._account, letter, this._character.GetId(), Now, () => this._context.SaveChangesAsync()).ConfigureAwait(false));
        Assert.That(this._account.WCoinC, Is.EqualTo(100));
        Assert.That(this._character.Inventory!.Money, Is.EqualTo(1000));
        Assert.That(this._character.Inventory.Items, Is.Empty);
        await this._context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await this._context.GetRepository<Item>().GetAllAsync().ConfigureAwait(false), Is.Empty);
        Assert.That(await this._context.GetCashShopCoinHistoryAsync(this._account.GetId(), 0, 20).ConfigureAwait(false), Is.Empty);
        Assert.That((await this.CollectAsync(letter).ConfigureAwait(false)).Status, Is.EqualTo("Applied"));
    }

    private Guid Send(GiftCodeReward? reward = null)
    {
        var letter = Guid.NewGuid();
        this._box.Send(letter, "host", "event compensation", [this._account.GetId()], "Event gift", "Quà sự kiện", "Thanks for joining.", "Cảm ơn bạn tham gia.", reward ?? new() { Coins = 50, CoinType = CashShopCoinType.WCoinC, Zen = 1000, Items = [new() { Count = 2, Level = 3 }] }, Now, Now.AddHours(1));
        return letter;
    }

    private ValueTask<GiftBoxCollectionResult> CollectAsync(Guid letter, DateTime? now = null) => this._collection.CollectAsync(this._context, this._account, letter, this._character.GetId(), now ?? Now, () => this._context.SaveChangesAsync());
}
