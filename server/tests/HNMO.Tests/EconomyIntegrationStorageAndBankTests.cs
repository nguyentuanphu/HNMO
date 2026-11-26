// <copyright file="EconomyIntegrationStorageAndBankTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using Moq;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.DataModel;
using HNMO.GameLogic.PlugIns.JewelBank;
using HNMO.Persistence;
using BasicModel = HNMO.Persistence.BasicModel;

/// <summary>
/// Economy paths where items or jewels could be lost or doubled: partial Jewel Bank deposits and withdrawals, a
/// pending withdrawal delivered in parts, and <see cref="Storage.TryTakeAllAsync"/> when it can't take everything
/// or a storage throws halfway.
/// </summary>
[TestFixture]
public class EconomyIntegrationStorageAndBankTests
{
    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated directory for the Jewel Bank state file.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-economy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes the directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>
    /// A deposit that would pass the capacity is refused as a whole: nothing of it is stored and no log line is
    /// written, so a retry with the part that fits gives exactly the capacity.
    /// </summary>
    [Test]
    public void DepositOverTheCapacityStoresNothingAndTheFittingPartCanFollow()
    {
        var engine = this.CreateEngine(capacity: 10);
        Assert.That(engine.Deposit("alice", "bless", 7, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        var logsBefore = engine.Store.Read(d => d.Log.Count);

        Assert.That(engine.Deposit("alice", "bless", 4, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.CapacityExceeded));
        Assert.That(engine.GetBalances("alice")["bless"], Is.EqualTo(7));
        Assert.That(engine.Store.Read(d => d.Log.Count), Is.EqualTo(logsBefore));

        Assert.That(engine.Deposit("alice", "bless", 3, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.GetBalances("alice")["bless"], Is.EqualTo(10));
    }

    /// <summary>Partial withdrawals take exactly what was asked; asking for more than is left changes nothing.</summary>
    [Test]
    public void PartialWithdrawalsLeaveTheRestAndAnOverdrawChangesNothing()
    {
        var engine = this.CreateEngine(capacity: 100);
        Assert.That(engine.Deposit("alice", "bless", 10, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));

        Assert.That(engine.TryWithdraw("alice", "bless", 3, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.TryWithdraw("alice", "bless", 4, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.GetBalances("alice")["bless"], Is.EqualTo(3));

        Assert.That(engine.TryWithdraw("alice", "bless", 4, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.InsufficientBalance));
        Assert.That(engine.GetBalances("alice")["bless"], Is.EqualTo(3));

        Assert.That(engine.TryWithdraw("alice", "bless", 3, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.GetBalances("alice").ContainsKey("bless"), Is.False, "an emptied jewel leaves no zero entry");
        Assert.That(engine.TryWithdraw("alice", "bless", 1, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.InsufficientBalance));
    }

    /// <summary>
    /// A website withdrawal waits as a pending entry; delivered in parts (a bag with room for only some), each part
    /// lowers it, and it is gone once all is delivered. Delivering zero or an unknown entry changes nothing.
    /// </summary>
    [Test]
    public void APendingWithdrawalDeliveredInPartsIsNeitherLostNorDoubled()
    {
        var engine = this.CreateEngine(capacity: 100);
        Assert.That(engine.Deposit("alice", "bless", 5, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.TryWithdraw("alice", "bless", 5, "website", DateTime.UtcNow, enqueuePending: true), Is.EqualTo(JewelBankResult.Ok));
        var id = engine.PeekPending("alice").Single().Id;

        engine.ConsumePending(id, 0);
        engine.ConsumePending("no-such-entry", 2);
        Assert.That(engine.PeekPending("alice").Single().Quantity, Is.EqualTo(5));

        engine.ConsumePending(id, 2);
        Assert.That(engine.PeekPending("alice").Single().Quantity, Is.EqualTo(3));
        engine.ConsumePending(id, 3);
        Assert.That(engine.PeekPending("alice"), Is.Empty);

        engine.ConsumePending(id, 3);
        Assert.That(engine.PeekPending("alice"), Is.Empty);
        Assert.That(engine.GetBalances("alice").ContainsKey("bless"), Is.False, "the bank balance isn't touched by deliveries");
    }

    /// <summary>The bank state survives a reopen of the state file (a server restart between deposit and withdrawal).</summary>
    [Test]
    public void BalancesAndPendingEntriesSurviveAReopen()
    {
        var engine = this.CreateEngine(capacity: 100);
        Assert.That(engine.Deposit("alice", "bless", 6, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        Assert.That(engine.TryWithdraw("alice", "bless", 2, "website", DateTime.UtcNow, enqueuePending: true), Is.EqualTo(JewelBankResult.Ok));

        var reopened = new JewelBankEngine(engine.Configuration, JewelBankStore.Open(engine.Configuration.FileName), enabled: true);

        Assert.That(reopened.GetBalances("alice")["bless"], Is.EqualTo(4));
        Assert.That(reopened.PeekPending("alice").Single().Quantity, Is.EqualTo(2));
    }

    /// <summary>
    /// <see cref="Storage.TryTakeAllAsync"/> into a storage without room for everything returns false and puts every
    /// item back where it was: nothing moves, nothing is lost.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryTakeAllWithoutRoomReturnsFalseAndMovesNothingAsync()
    {
        var destination = new Storage(12 + 64, 12, 0, CreateItemStorage());
        for (byte slot = 13; slot < 12 + 64; slot++)
        {
            Assert.That(await destination.AddItemAsync(slot, GetItem()).ConfigureAwait(false), Is.True);
        }

        var source = new Storage(12 + 64, 12, 0, CreateItemStorage());
        var first = GetItem();
        var second = GetItem();
        var third = GetItem();
        Assert.That(await source.AddItemAsync(12, first).ConfigureAwait(false), Is.True);
        Assert.That(await source.AddItemAsync(20, second).ConfigureAwait(false), Is.True);
        Assert.That(await source.AddItemAsync(30, third).ConfigureAwait(false), Is.True);
        var destinationCount = destination.Items.Count();

        Assert.That(await destination.TryTakeAllAsync(source).ConfigureAwait(false), Is.False);

        Assert.That(destination.Items.Count(), Is.EqualTo(destinationCount));
        Assert.That(source.GetItem(12), Is.SameAs(first));
        Assert.That(source.GetItem(20), Is.SameAs(second));
        Assert.That(source.GetItem(30), Is.SameAs(third));
    }

    /// <summary>
    /// When the destination throws while adding the second item, no item may be lost: each one is in exactly one of
    /// the two storages afterwards.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryTakeAllThatThrowsHalfwayLosesNoItemAsync()
    {
        var source = new Storage(12 + 64, 12, 0, CreateItemStorage());
        var first = GetItem();
        var second = GetItem();
        Assert.That(await source.AddItemAsync(12, first).ConfigureAwait(false), Is.True);
        Assert.That(await source.AddItemAsync(13, second).ConfigureAwait(false), Is.True);
        var destination = new Storage(12 + 64, 12, 0, CreateItemStorage(new ThrowingOnSecondAdd()));

        Assert.That(async () => await destination.TryTakeAllAsync(source).ConfigureAwait(false), Throws.InvalidOperationException);

        foreach (var item in new[] { first, second })
        {
            var places = (source.Items.Contains(item) ? 1 : 0) + (destination.Items.Contains(item) ? 1 : 0);
            Assert.That(places, Is.EqualTo(1), "an item that left the source must be in the destination, or back in the source");
        }
    }

    private static ItemStorage CreateItemStorage(ICollection<Item>? items = null)
    {
        var storage = new Mock<ItemStorage>();
        storage.Setup(s => s.Items).Returns(items ?? new List<Item>());
        return storage.Object;
    }

    private static Item GetItem()
    {
        var item = new Item { Definition = new ItemDefinition { Durability = 1, Width = 1, Height = 1 } };
        item.Durability = 1;
        return item;
    }

    private JewelBankEngine CreateEngine(int capacity)
    {
        var configuration = new JewelBankConfiguration
        {
            FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
            Jewels = [new() { Key = "bless", NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện", Group = 14, Number = 13, Enabled = true, MaxCapacity = capacity }],
        };
        return new JewelBankEngine(configuration, JewelBankStore.Open(configuration.FileName), enabled: true);
    }

    /// <summary>The saved item list of a storage whose second add throws, like a database fault in the middle of a move.</summary>
    private sealed class ThrowingOnSecondAdd : System.Collections.ObjectModel.Collection<Item>
    {
        private int _adds;

        protected override void InsertItem(int index, Item item)
        {
            if (++this._adds == 2)
            {
                throw new InvalidOperationException("fault while adding");
            }

            base.InsertItem(index, item);
        }
    }
}

/// <summary>
/// The Jewel Bank plug-in around the bank write: a refused write leaves the bag as it was and refunds the fee, a
/// partial deposit keeps the rest in the bag, a withdrawal into a bag with room for some puts the rest back in the
/// bank, and a failed save after the write neither loses nor doubles a jewel in the running game.
/// </summary>
[TestFixture]
public class EconomyIntegrationJewelBankPlugInTests
{
    private static readonly byte FirstBagSlot = InventoryConstants.EquippableSlotsCount;

    private string _tempDir = string.Empty;

    /// <summary>Creates an isolated directory for the Jewel Bank state file.</summary>
    [SetUp]
    public void SetUp()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), $"hnmo-economy-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._tempDir);
    }

    /// <summary>Deletes the directory.</summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this._tempDir))
        {
            Directory.Delete(this._tempDir, recursive: true);
        }
    }

    /// <summary>A deposit into a full bank writes nothing, leaves the stack in the bag and gives the Zen fee back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DepositIntoAFullBankKeepsTheStackAndRefundsTheFeeAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 5, depositFee: 1_000).ConfigureAwait(false);
        await SeedBankAsync(player, 5).ConfigureAwait(false);
        var stack = await AddStackAsync(player, bless, FirstBagSlot, 3).ConfigureAwait(false);
        player.TryAddMoney(5_000);

        await plugIn.DepositAsync(player, FirstBagSlot, depositAll: false).ConfigureAwait(false);

        Assert.That(await BankAsync(player).ConfigureAwait(false), Is.EqualTo(5));
        Assert.That(player.Inventory!.GetItem(FirstBagSlot), Is.SameAs(stack));
        Assert.That(stack.Durability, Is.EqualTo(3));
        Assert.That(player.Money, Is.EqualTo(5_000));
    }

    /// <summary>A stack bigger than the room left goes in partly; the rest stays in the bag, so the total is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialDepositKeepsTheRestOfTheStackInTheBagAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 5, depositFee: 0).ConfigureAwait(false);
        await SeedBankAsync(player, 2).ConfigureAwait(false);
        var stack = await AddStackAsync(player, bless, FirstBagSlot, 5).ConfigureAwait(false);

        await plugIn.DepositAsync(player, FirstBagSlot, depositAll: false).ConfigureAwait(false);

        Assert.That(await BankAsync(player).ConfigureAwait(false), Is.EqualTo(5));
        Assert.That(player.Inventory!.GetItem(FirstBagSlot), Is.SameAs(stack));
        Assert.That(stack.Durability, Is.EqualTo(2));
    }

    /// <summary>A deposit commits the bank, the bag and one receipt together; a normal save after it changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADepositCommitsTheBankTheBagAndOneReceiptTogetherAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 100, depositFee: 1_000).ConfigureAwait(false);
        await AddStackAsync(player, bless, FirstBagSlot, 4).ConfigureAwait(false);
        player.TryAddMoney(5_000);
        var receipts = new List<AssetOperationRequest>();
        plugIn.AfterCommit = request =>
        {
            receipts.Add(request);
            return ValueTask.CompletedTask;
        };

        await plugIn.DepositAsync(player, FirstBagSlot, depositAll: false).ConfigureAwait(false);
        Assert.That(await player.SaveProgressAsync().ConfigureAwait(false), Is.True);

        Assert.Multiple(async () =>
        {
            Assert.That(await BankAsync(player).ConfigureAwait(false), Is.EqualTo(4));
            Assert.That(InBag(player, bless), Is.Zero);
            Assert.That(player.Money, Is.EqualTo(4_000));
            Assert.That(receipts, Has.Count.EqualTo(1));
            Assert.That(await player.PersistenceContext.GetAssetOperationAsync(receipts[0].OperationId).ConfigureAwait(false), Is.Not.Null);
        });
    }

    /// <summary>A deposit whose commit fails banks nothing: the jewels and the fee are back in the bag.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADepositWhoseCommitFailsBanksNothingAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 100, depositFee: 1_000).ConfigureAwait(false);
        await AddStackAsync(player, bless, FirstBagSlot, 4).ConfigureAwait(false);
        player.TryAddMoney(5_000);
        plugIn.OutcomeReadDelay = TimeSpan.Zero;
        var fails = true;
        CashShopActionsTest.SaveFailingContext.Install(player, () => fails);

        await plugIn.DepositAsync(player, FirstBagSlot, depositAll: false).ConfigureAwait(false);
        fails = false;

        Assert.Multiple(async () =>
        {
            Assert.That(await BankAsync(player).ConfigureAwait(false), Is.Zero);
            Assert.That(InBag(player, bless), Is.EqualTo(4));
            Assert.That(player.Money, Is.EqualTo(5_000));
        });
    }

    /// <summary>
    /// The answer of a deposit's commit is lost after it went through: the outcome read from the database counts, so
    /// the jewels are banked once and not given back.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADepositWhoseCommitAnswerIsLostCountsOnceAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 100, depositFee: 0).ConfigureAwait(false);
        await AddStackAsync(player, bless, FirstBagSlot, 4).ConfigureAwait(false);
        plugIn.OutcomeReadDelay = TimeSpan.Zero;
        plugIn.AfterCommit = _ => throw new InvalidOperationException("the answer is lost");

        await plugIn.DepositAsync(player, FirstBagSlot, depositAll: false).ConfigureAwait(false);

        Assert.That(await BankAsync(player).ConfigureAwait(false), Is.EqualTo(4));
        Assert.That(InBag(player, bless), Is.Zero);
    }

    /// <summary>A withdrawal of five into a bag with two free slots gives two and leaves three in the bank.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithdrawalIntoABagWithRoomForSomeLeavesTheRestInTheBankAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 100, depositFee: 0).ConfigureAwait(false);
        await SeedBankAsync(player, 5).ConfigureAwait(false);
        await FillBagAsync(player, freeSlots: 2).ConfigureAwait(false);

        await plugIn.WithdrawAsync(player, "bless", 5).ConfigureAwait(false);

        Assert.That(InBag(player, bless), Is.EqualTo(2));
        Assert.That(await BankAsync(player).ConfigureAwait(false), Is.EqualTo(3));
    }

    /// <summary>A withdrawal whose commit fails takes nothing: the jewels leave the bag again, the fee comes back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AWithdrawalWhoseCommitFailsTakesNothingAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 100, depositFee: 0, withdrawFee: 1_000).ConfigureAwait(false);
        await SeedBankAsync(player, 5).ConfigureAwait(false);
        await AddStackAsync(player, bless, FirstBagSlot, 2).ConfigureAwait(false); // a stack the jewels may join
        player.TryAddMoney(5_000);
        plugIn.OutcomeReadDelay = TimeSpan.Zero;
        var fails = true;
        CashShopActionsTest.SaveFailingContext.Install(player, () => fails);

        await plugIn.WithdrawAsync(player, "bless", 3).ConfigureAwait(false);
        fails = false;

        Assert.Multiple(async () =>
        {
            Assert.That(await BankAsync(player).ConfigureAwait(false), Is.EqualTo(5));
            Assert.That(InBag(player, bless), Is.EqualTo(2));
            Assert.That(player.Money, Is.EqualTo(5_000));
        });
    }

    /// <summary>
    /// The website takes jewels from the bank while a withdrawal in the game is on its way: the game's debit is refused
    /// at the commit, so the bank can't go below zero, and the jewels leave the bag again.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AWebsiteWithdrawalInBetweenCantBeTakenTwiceAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 100, depositFee: 0).ConfigureAwait(false);
        await SeedBankAsync(player, 5).ConfigureAwait(false);
        plugIn.BeforeCommit = async _ =>
        {
            plugIn.BeforeCommit = null;
            using var website = player.GameContext.PersistenceContextProvider.CreateNewPlayerContext(player.GameContext.Configuration);
            var webWithdraw = new AssetOperationRequest(Guid.NewGuid(), player.Account!.GetId(), JewelBankPlugIn.WebsiteWithdrawKind, "w", "website", null, [new JewelBankDelta("bless", -4, int.MaxValue)], "bless", 4);
            Assert.That(await website.SaveChangesWithAssetOperationAsync(webWithdraw).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Committed));
        };

        await plugIn.WithdrawAsync(player, "bless", 3).ConfigureAwait(false);

        Assert.That(InBag(player, bless), Is.Zero);
        Assert.That(await BankAsync(player).ConfigureAwait(false), Is.EqualTo(1));
    }

    /// <summary>A website withdrawal waits for room: each login delivers what fits and counts it once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AWaitingWebsiteWithdrawalIsDeliveredInPartsAndOnceAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 100, depositFee: 0).ConfigureAwait(false);
        await SeedBankAsync(player, 5).ConfigureAwait(false);
        var queued = new AssetOperationRequest(Guid.NewGuid(), player.Account!.GetId(), JewelBankPlugIn.WebsiteWithdrawKind, "w", "website", null, [new JewelBankDelta("bless", -5, int.MaxValue)], "bless", 5);
        Assert.That(await player.PersistenceContext.SaveChangesWithAssetOperationAsync(queued).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Committed));
        var fillers = await FillBagAsync(player, freeSlots: 2).ConfigureAwait(false);

        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.EnteredWorld).ConfigureAwait(false);
        Assert.That(InBag(player, bless), Is.EqualTo(2));
        Assert.That((await player.PersistenceContext.GetAssetOperationAsync(queued.OperationId).ConfigureAwait(false))!.PendingCount, Is.EqualTo(3));

        foreach (var filler in fillers)
        {
            await player.Inventory!.RemoveItemAsync(filler).ConfigureAwait(false);
        }

        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.EnteredWorld).ConfigureAwait(false);
        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.EnteredWorld).ConfigureAwait(false);

        Assert.That(InBag(player, bless), Is.EqualTo(5));
        Assert.That(await player.PersistenceContext.GetPendingAssetOperationsAsync(player.Account!.GetId()).ConfigureAwait(false), Is.Empty);
        Assert.That(await BankAsync(player).ConfigureAwait(false), Is.Zero);
    }

    private static int InBag(Player player, BasicModel.ItemDefinition definition)
        => player.Inventory!.Items.Where(i => i.Definition == definition).Sum(i => (int)i.Durability);

    private static async Task<int> BankAsync(Player player)
        => (await player.PersistenceContext.GetJewelBankBalancesAsync(player.Account!.GetId()).ConfigureAwait(false)).GetValueOrDefault("bless");

    private static async Task SeedBankAsync(Player player, int count)
    {
        var seed = new AssetOperationRequest(Guid.NewGuid(), player.Account!.GetId(), "jewel-deposit", "seed", "test", null, [new JewelBankDelta("bless", count, int.MaxValue)]);
        Assert.That(await player.PersistenceContext.SaveChangesWithAssetOperationAsync(seed).ConfigureAwait(false), Is.EqualTo(AssetCommitStatus.Committed));
    }

    private static async Task<List<Item>> FillBagAsync(Player player, int freeSlots)
    {
        var filler = new BasicModel.ItemDefinition { Group = 14, Number = 0, Width = 1, Height = 1, Durability = 1 };
        player.GameContext.Configuration.Items.Add(filler);
        var items = new List<Item>();
        for (var slot = FirstBagSlot + freeSlots; slot < FirstBagSlot + 64; slot++)
        {
            items.Add(await AddStackAsync(player, filler, (byte)slot, 1).ConfigureAwait(false));
        }

        return items;
    }

    private static async Task<Item> AddStackAsync(Player player, BasicModel.ItemDefinition definition, byte slot, int count)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Durability = (byte)count;
        Assert.That(await player.Inventory!.AddItemAsync(slot, item).ConfigureAwait(false), Is.True);
        return item;
    }

    private async Task<(JewelBankPlugIn PlugIn, Player Player, BasicModel.ItemDefinition Bless)> CreateAsync(int capacity, int depositFee, int withdrawFee = 0)
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account!.LoginName = "alice";
        var bless = new BasicModel.ItemDefinition { Group = 14, Number = 13, Width = 1, Height = 1, Durability = 1 };
        context.Configuration.Items.Add(bless);
        var plugIn = new JewelBankPlugIn
        {
            Configuration = new JewelBankConfiguration
            {
                FileName = Path.Combine(this._tempDir, "hnmo-state.json"),
                SafeZoneOnly = false,
                DepositZenFee = depositFee,
                WithdrawZenFee = withdrawFee,
                Jewels = [new() { Key = "bless", NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện", Group = 14, Number = 13, Enabled = true, MaxCapacity = capacity }],
            },
        };
        return (plugIn, player, bless);
    }
}
