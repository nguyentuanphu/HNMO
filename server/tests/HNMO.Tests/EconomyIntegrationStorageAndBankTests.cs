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
        Assert.That(plugIn.GetEngine().Deposit("alice", "bless", 5, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        var stack = await AddStackAsync(player, bless, FirstBagSlot, 3).ConfigureAwait(false);
        player.TryAddMoney(5_000);

        await plugIn.DepositAsync(player, FirstBagSlot, depositAll: false).ConfigureAwait(false);

        Assert.That(plugIn.GetEngine().GetBalances("alice")["bless"], Is.EqualTo(5));
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
        Assert.That(plugIn.GetEngine().Deposit("alice", "bless", 2, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        var stack = await AddStackAsync(player, bless, FirstBagSlot, 5).ConfigureAwait(false);

        await plugIn.DepositAsync(player, FirstBagSlot, depositAll: false).ConfigureAwait(false);

        Assert.That(plugIn.GetEngine().GetBalances("alice")["bless"], Is.EqualTo(5));
        Assert.That(player.Inventory!.GetItem(FirstBagSlot), Is.SameAs(stack));
        Assert.That(stack.Durability, Is.EqualTo(2));
    }

    /// <summary>A withdrawal of five into a bag with two free slots gives two and puts three back in the bank.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithdrawalIntoABagWithRoomForSomeReturnsTheRestToTheBankAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 100, depositFee: 0).ConfigureAwait(false);
        Assert.That(plugIn.GetEngine().Deposit("alice", "bless", 5, "game", DateTime.UtcNow), Is.EqualTo(JewelBankResult.Ok));
        var filler = new BasicModel.ItemDefinition { Group = 14, Number = 0, Width = 1, Height = 1, Durability = 1 };
        player.GameContext.Configuration.Items.Add(filler);
        for (var slot = FirstBagSlot + 2; slot < FirstBagSlot + 64; slot++)
        {
            await AddStackAsync(player, filler, (byte)slot, 1).ConfigureAwait(false);
        }

        await plugIn.WithdrawAsync(player, "bless", 5).ConfigureAwait(false);

        var pieces = player.Inventory!.Items.Where(i => i.Definition == bless).Sum(i => (int)i.Durability);
        Assert.That(pieces, Is.EqualTo(2));
        Assert.That(plugIn.GetEngine().GetBalances("alice")["bless"], Is.EqualTo(3));
    }

    /// <summary>
    /// When the save after a deposit fails, the running game still holds every jewel exactly once (bank plus bag).
    /// What a later login sees depends on the next save; a server crash between the bank file write and a good
    /// save is not covered here (it needs a real database).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFailedSaveAfterTheDepositNeitherLosesNorDoublesAJewelAsync()
    {
        var (plugIn, player, bless) = await this.CreateAsync(capacity: 100, depositFee: 0).ConfigureAwait(false);
        await AddStackAsync(player, bless, FirstBagSlot, 4).ConfigureAwait(false);
        CashShopActionsTest.SaveFailingContext.Install(player, () => true);

        try
        {
            await plugIn.DepositAsync(player, FirstBagSlot, depositAll: false).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // the failing save may surface here; what counts is the state below
        }

        var inBag = player.Inventory!.Items.Where(i => i.Definition == bless).Sum(i => (int)i.Durability);
        plugIn.GetEngine().GetBalances("alice").TryGetValue("bless", out var inBank);
        Assert.That(inBag + inBank, Is.EqualTo(4));
    }

    private static async Task<Item> AddStackAsync(Player player, BasicModel.ItemDefinition definition, byte slot, int count)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Durability = (byte)count;
        Assert.That(await player.Inventory!.AddItemAsync(slot, item).ConfigureAwait(false), Is.True);
        return item;
    }

    private async Task<(JewelBankPlugIn PlugIn, Player Player, BasicModel.ItemDefinition Bless)> CreateAsync(int capacity, int depositFee)
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
                Jewels = [new() { Key = "bless", NameEn = "Jewel of Bless", NameVi = "Ngọc Ước Nguyện", Group = 14, Number = 13, Enabled = true, MaxCapacity = capacity }],
            },
        };
        return (plugIn, player, bless);
    }
}
