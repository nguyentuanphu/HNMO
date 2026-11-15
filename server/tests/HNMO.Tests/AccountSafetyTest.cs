// <copyright file="AccountSafetyTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using Moq;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.GameLogic.PlayerActions.Vault;
using HNMO.GameLogic.PlugIns.AccountSafety;
using HNMO.GameLogic.Views.Trade;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Tests for HNMO's account safety: the second password with its wrong-try limits, the item lock with its unlock
/// wait, the login and trade history (<see cref="AccountSafetyEngine"/>), and in the game: locked items kept out of
/// trades, sales, drops, stores and mixes, the second password for trades and the vault, and the trade history.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class AccountSafetyTest
{
    private const byte BagSlot = 20;
    private static readonly DateTime Now = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    private string _file = null!;

    /// <summary>
    /// Uses a quick hash and its own state file.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        AccountSafetyEngine.WorkFactor = 4;
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-safety-test-{Guid.NewGuid():N}.json");
    }

    /// <summary>
    /// Deletes the state file.
    /// </summary>
    [TearDown]
    public void TearDown() => File.Delete(this._file);

    /// <summary>
    /// A PIN is four digits (leading zeros count like the game's number); it's kept hashed, and the plain PINs of
    /// older versions still match.
    /// </summary>
    [Test]
    public void PinsAreFourDigitsAndHashed()
    {
        Assert.That(AccountSafetyEngine.NormalizePin("1234"), Is.EqualTo("1234"));
        Assert.That(AccountSafetyEngine.NormalizePin("0042"), Is.EqualTo("42"));
        Assert.That(AccountSafetyEngine.NormalizePin("123"), Is.Null);
        Assert.That(AccountSafetyEngine.NormalizePin("12a4"), Is.Null);
        Assert.That(AccountSafetyEngine.PinOf(42), Is.EqualTo("42"));
        Assert.That(AccountSafetyEngine.PinOf(0xFFFF), Is.Null);

        var hash = AccountSafetyEngine.HashPin("42");
        Assert.That(hash, Does.StartWith("$2"));
        Assert.That(AccountSafetyEngine.PinMatches(hash, "42"), Is.True);
        Assert.That(AccountSafetyEngine.PinMatches(hash, "43"), Is.False);
        Assert.That(AccountSafetyEngine.PinMatches("1234", "1234"), Is.True, "a plain PIN of an older version");
        Assert.That(AccountSafetyEngine.PinMatches(string.Empty, "1234"), Is.False);
    }

    /// <summary>
    /// Wrong second passwords count down to a lockout; during it even the right one is refused; after it the tries
    /// start again; a right one forgets the wrong ones.
    /// </summary>
    [Test]
    public void WrongTriesLockTheAccountOut()
    {
        var engine = this.Engine(new AccountSafetyConfiguration { MaxWrongTries = 3, LockoutMinutes = 30 });
        var stored = AccountSafetyEngine.HashPin("1234");

        Assert.That(engine.CheckPin("test1", stored, "1111", Now), Is.EqualTo(new PinCheck(PinCheckResult.Wrong, 2, null)));
        Assert.That(engine.CheckPin("test1", stored, "1234", Now).Result, Is.EqualTo(PinCheckResult.Ok));
        Assert.That(engine.CheckPin("test1", stored, "1111", Now).TriesLeft, Is.EqualTo(2), "the right one forgot the wrong one");
        Assert.That(engine.CheckPin("test1", stored, null, Now).TriesLeft, Is.EqualTo(1), "not four digits counts as wrong");
        var lockedOut = engine.CheckPin("TEST1", stored, "2222", Now);
        Assert.That(lockedOut.Result, Is.EqualTo(PinCheckResult.LockedOut));
        Assert.That(lockedOut.LockedOutUntilUtc, Is.EqualTo(Now.AddMinutes(30)));
        Assert.That(engine.CheckPin("test1", stored, "1234", Now.AddMinutes(29)).Result, Is.EqualTo(PinCheckResult.LockedOut));
        Assert.That(engine.LockedOutUntil("test1", Now.AddMinutes(10)), Is.EqualTo(Now.AddMinutes(30)));
        Assert.That(engine.CheckPin("test1", stored, "1234", Now.AddMinutes(31)).Result, Is.EqualTo(PinCheckResult.Ok));
        Assert.That(engine.CheckPin("test2", string.Empty, "1234", Now).Result, Is.EqualTo(PinCheckResult.NoPin));

        engine.CheckPin("test3", stored, "0", Now);
        engine.CheckPin("test3", stored, "0", Now);
        engine.CheckPin("test3", stored, "0", Now);
        engine.ClearLockout("test3");
        Assert.That(engine.CheckPin("test3", stored, "1234", Now).Result, Is.EqualTo(PinCheckResult.Ok), "the admin cleared the lockout");
    }

    /// <summary>
    /// A locked item stays locked during its unlock wait and is free after it; locking it again cancels the unlock;
    /// without a wait the unlock is at once; the number of locks is limited; the state outlasts a restart.
    /// </summary>
    [Test]
    public void ItemLocksWaitForTheirUnlock()
    {
        var configuration = new AccountSafetyConfiguration { UnlockWaitHours = 24, MaxLockedItems = 2 };
        var engine = this.Engine(configuration);
        var sword = Guid.NewGuid();
        var shield = Guid.NewGuid();

        Assert.That(engine.Lock("test1", sword, "Kris +7", "test1Dk", Now), Is.EqualTo(ItemLockResult.Locked));
        Assert.That(engine.Lock("test1", sword, "Kris +7", "test1Dk", Now), Is.EqualTo(ItemLockResult.AlreadyLocked));
        Assert.That(engine.IsLocked("test1", sword, Now), Is.True);
        Assert.That(engine.IsLocked("test2", sword, Now), Is.False, "locks are per account");

        Assert.That(engine.Unlock("test1", sword, Now), Is.EqualTo((ItemLockResult.UnlockStarted, (DateTime?)Now.AddHours(24))));
        Assert.That(engine.Unlock("test1", sword, Now.AddHours(1)).Result, Is.EqualTo(ItemLockResult.UnlockWaiting));
        Assert.That(engine.IsLocked("test1", sword, Now.AddHours(23)), Is.True, "still locked while the unlock waits");
        Assert.That(engine.Lock("test1", sword, "Kris +7", "test1Dk", Now.AddHours(23)), Is.EqualTo(ItemLockResult.UnlockCancelled));
        Assert.That(engine.IsLocked("test1", sword, Now.AddHours(25)), Is.True, "the unlock was cancelled");

        engine.Unlock("test1", sword, Now.AddHours(25));
        Assert.That(engine.IsLocked("test1", sword, Now.AddHours(49).AddMinutes(1)), Is.False);
        Assert.That(engine.Locks("test1", Now.AddHours(50)), Is.Empty);

        engine.Lock("test1", sword, "Kris +7", "test1Dk", Now);
        engine.Lock("test1", shield, "Shield", "test1Dk", Now);
        Assert.That(engine.Lock("test1", Guid.NewGuid(), "Third", "test1Dk", Now), Is.EqualTo(ItemLockResult.TooMany));
        Assert.That(engine.RemoveLock("test1", shield), Is.True);
        this.Save(engine);

        var restarted = new AccountSafetyEngine(new AccountSafetyConfiguration { UnlockWaitHours = 0 }, new AccountSafetyStore(this._file));
        Assert.That(restarted.IsLocked("test1", sword, Now), Is.True, "the lock outlasts a restart");
        Assert.That(restarted.Unlock("test1", sword, Now).Result, Is.EqualTo(ItemLockResult.Unlocked), "no wait: at once");
        Assert.That(restarted.IsLocked("test1", sword, Now), Is.False);
        Assert.That(new AccountSafetyEngine(new AccountSafetyConfiguration { ItemLockEnabled = false }, new AccountSafetyStore()).Lock("a", sword, "x", "y", Now), Is.EqualTo(ItemLockResult.Off));
    }

    /// <summary>
    /// Logins are kept for the configured days, failures only for known accounts; the previous successful login tells
    /// whether this one came from another address; the website masks addresses.
    /// </summary>
    [Test]
    public void LoginHistoryIsKeptAndComparesAddresses()
    {
        var engine = this.Engine(new AccountSafetyConfiguration { LoginHistoryDays = 30 });

        Assert.That(engine.RecordLogin("nobody", "1.2.3.4", "game", false, Now), Is.Null);
        Assert.That(engine.Logins("nobody", 10), Is.Empty, "a failure of an unknown account isn't kept");

        Assert.That(engine.RecordLogin("test1", "203.0.113.7", "game", true, Now.AddDays(-40)), Is.Null);
        var previous = engine.RecordLogin("test1", "203.0.113.7", "website", true, Now.AddDays(-1));
        Assert.That(previous?.Address, Is.EqualTo("203.0.113.7"));
        engine.RecordLogin("test1", "198.51.100.9", "game", false, Now.AddHours(-1));
        previous = engine.RecordLogin("test1", "198.51.100.9", "game", true, Now);
        Assert.That((previous?.Source, previous?.Success), Is.EqualTo(("website", (bool?)true)), "the previous successful login");
        Assert.That(AccountSafetyEngine.IsNewAddress(previous, "198.51.100.9"), Is.True);
        Assert.That(AccountSafetyEngine.IsNewAddress(previous, "203.0.113.7"), Is.False);
        Assert.That(AccountSafetyEngine.IsNewAddress(null, "203.0.113.7"), Is.False);

        var logins = engine.Logins("test1", 10);
        Assert.That(logins.Select(l => (l.Address, l.Source, l.Success)), Is.EqualTo(new[]
        {
            ("198.51.100.9", "game", true), ("198.51.100.9", "game", false), ("203.0.113.7", "website", true),
        }), "newest first; the one older than 30 days is gone");
        Assert.That(AccountSafetyEngine.Mask("203.0.113.7"), Is.EqualTo("203.0.x.x"));
        Assert.That(AccountSafetyEngine.Mask("::ffff:203.0.113.7"), Is.EqualTo("203.0.x.x"));
        Assert.That(AccountSafetyEngine.Mask("2001:db8::1"), Is.EqualTo("2001:db8:x:x"));
        Assert.That(AccountSafetyEngine.Mask(null), Is.EqualTo("?"));
    }

    /// <summary>
    /// Trades are kept newest first for the configured days.
    /// </summary>
    [Test]
    public void TradeHistoryIsKept()
    {
        var engine = this.Engine(new AccountSafetyConfiguration { TradeHistoryDays = 10 });
        engine.RecordTrade("test1", new SafetyTradeEntry { Character = "a", Partner = "old" }, Now.AddDays(-11));
        engine.RecordTrade("test1", new SafetyTradeEntry { Character = "a", Partner = "b", Gave = { "Kris" }, ZenGot = 5 }, Now);
        Assert.That(engine.Trades("test1", 10).Select(t => (t.Partner, t.ZenGot, t.Gave.FirstOrDefault())), Is.EqualTo(new[] { ("b", 5L, "Kris") }));
        Assert.That(engine.Trades("test1", 0), Is.Empty);
    }

    /// <summary>
    /// A locked item isn't dropped, sold to a merchant, or moved into a trade, a personal store or the chaos machine;
    /// an unlocked one is (the drop).
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask LockedItemIsBlockedEverywhereAsync()
    {
        var context = this.Context();
        var player = await this.PlayerAsync(context, "seller").ConfigureAwait(false);
        var engine = AccountSafetyPlugIn.Get(context)!.GetEngine();
        var item = CreateItem();
        await player.Inventory!.AddItemAsync(BagSlot, item).ConfigureAwait(false);
        engine.Lock("seller", item.Id, "Kris", "seller", DateTime.UtcNow);

        await new DropItemAction().DropItemAsync(player, BagSlot, new Point(10, 10)).ConfigureAwait(false);
        Assert.That(player.Inventory.GetItem(BagSlot), Is.SameAs(item), "not dropped");

        player.OpenedNpc = new NonPlayerCharacter(null!, new MonsterDefinition { MerchantStore = new Mock<ItemStorage>().Object }, null!);
        Assert.That(await new SellItemToNpcAction().SellItemAsync(player, BagSlot).ConfigureAwait(false), Is.False, "not sold");
        player.OpenedNpc = null;

        await new MoveItemAction().MoveItemAsync(player, BagSlot, Storages.Inventory, InventoryConstants.FirstStoreItemSlotIndex, Storages.PersonalStore).ConfigureAwait(false);
        Assert.That(player.Inventory.GetItem(BagSlot), Is.SameAs(item), "not put in the personal store");

        player.OpenedNpc = new NonPlayerCharacter(null!, new MonsterDefinition { NpcWindow = NpcWindow.ChaosMachine }, null!);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.NpcDialogOpened).ConfigureAwait(false);
        await new MoveItemAction().MoveItemAsync(player, BagSlot, Storages.Inventory, 0, Storages.ChaosMachine).ConfigureAwait(false);
        Assert.That(player.Inventory.GetItem(BagSlot), Is.SameAs(item), "not put in the chaos machine");
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        player.OpenedNpc = null;

        var partner = await this.PlayerAsync(context, "buyer").ConfigureAwait(false);
        await new TradeRequestAction().RequestTradeAsync(player, partner).ConfigureAwait(false);
        await new TradeAcceptAction().HandleTradeAcceptAsync(partner, true).ConfigureAwait(false);
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.TradeOpened));
        await new MoveItemAction().MoveItemAsync(player, BagSlot, Storages.Inventory, 0, Storages.Trade).ConfigureAwait(false);
        Assert.That(player.Inventory.GetItem(BagSlot), Is.SameAs(item), "not put in the trade");

        engine.RemoveLock("seller", item.Id);
        await new TradeCancelAction().CancelTradeAsync(player).ConfigureAwait(false);
        await new DropItemAction().DropItemAsync(player, BagSlot, new Point(10, 10)).ConfigureAwait(false);
        Assert.That(player.Inventory.GetItem(BagSlot), Is.Null, "dropped once unlocked");
    }

    /// <summary>
    /// A trade which holds an item locked after it went in is cancelled; a personal store item locked after it went
    /// in isn't sold; a mix with a locked item is refused.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask ItemLockedLaterIsStillBlockedAsync()
    {
        var context = this.Context();
        var trader1 = await this.PlayerAsync(context, "trader1").ConfigureAwait(false);
        var trader2 = await this.PlayerAsync(context, "trader2").ConfigureAwait(false);
        var engine = AccountSafetyPlugIn.Get(context)!.GetEngine();

        await new TradeRequestAction().RequestTradeAsync(trader1, trader2).ConfigureAwait(false);
        await new TradeAcceptAction().HandleTradeAcceptAsync(trader2, true).ConfigureAwait(false);
        var offered = CreateItem();
        await trader1.TemporaryStorage!.AddItemAsync(0, offered).ConfigureAwait(false);
        engine.Lock("trader1", offered.Id, "Kris", "trader1", DateTime.UtcNow);
        var buttons = new TradeButtonAction();
        await buttons.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await buttons.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);
        Assert.That(trader2.Inventory!.Items, Does.Not.Contain(offered));
        Assert.That(trader1.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));

        var stocked = CreateItem();
        stocked.StorePrice = 100;
        trader2.Money = 1_000_000;
        await trader1.ShopStorage!.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, stocked).ConfigureAwait(false);
        trader1.ShopStorage.StoreOpen = true;
        engine.Lock("trader1", stocked.Id, "Shield", "trader1", DateTime.UtcNow);
        await new BuyRequestAction().BuyItemAsync(trader2, trader1, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);
        Assert.That(trader1.ShopStorage.GetItem(InventoryConstants.FirstStoreItemSlotIndex), Is.SameAs(stocked), "not sold from the store");
        Assert.That(trader2.Money, Is.EqualTo(1_000_000));
    }

    /// <summary>
    /// With a second password set, a trade needs it from both traders; the vault's PIN unlock enters it (wrong ones
    /// count); after the trade both accounts have it in their trade history.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask TradeNeedsTheSecondPasswordAndIsRecordedAsync()
    {
        var context = this.Context();
        var trader1 = await this.PlayerAsync(context, "trader1", "1234").ConfigureAwait(false);
        var trader2 = await this.PlayerAsync(context, "trader2").ConfigureAwait(false);
        var safety = AccountSafetyPlugIn.Get(context)!;
        Assert.That(trader1.IsVaultLocked, Is.True);
        Assert.That(safety.IsEntered(trader1), Is.False);
        Assert.That(safety.IsEntered(trader2), Is.True, "no second password: nothing to enter");

        Assert.That(await new TradeRequestAction().RequestTradeAsync(trader1, trader2).ConfigureAwait(false), Is.False, "refused without the second password");
        Assert.That(await new TradeRequestAction().RequestTradeAsync(trader2, trader1).ConfigureAwait(false), Is.False, "the partner needs it too");

        await new UnlockVaultAction().UnlockVaultAsync(trader1, "1111").ConfigureAwait(false);
        Assert.That(safety.IsEntered(trader1), Is.False);
        Assert.That(safety.GetEngine().CheckPin("trader1", trader1.Account!.VaultPassword, "1111", DateTime.UtcNow).TriesLeft, Is.EqualTo(3), "the vault's wrong PIN counted");
        await new UnlockVaultAction().UnlockVaultAsync(trader1, "1234").ConfigureAwait(false);
        Assert.That(safety.IsEntered(trader1), Is.True);
        Assert.That(trader1.IsVaultLocked, Is.False);

        Assert.That(await new TradeRequestAction().RequestTradeAsync(trader1, trader2).ConfigureAwait(false), Is.True);
        await new TradeAcceptAction().HandleTradeAcceptAsync(trader2, true).ConfigureAwait(false);
        var offered = CreateItem();
        await trader1.TemporaryStorage!.AddItemAsync(0, offered).ConfigureAwait(false);
        trader2.TradingMoney = 500;
        trader2.Money = 1000;
        var buttons = new TradeButtonAction();
        await buttons.TradeButtonChangedAsync(trader1, TradeButtonState.Checked).ConfigureAwait(false);
        await buttons.TradeButtonChangedAsync(trader2, TradeButtonState.Checked).ConfigureAwait(false);
        Assert.That(trader2.Inventory!.Items, Does.Contain(offered));

        var mine = safety.GetEngine().Trades("trader1", 5).Single();
        Assert.That((mine.Kind, mine.Partner, mine.Gave.Single(), mine.ZenGot, mine.PartnerLogin), Is.EqualTo(("trade", trader2.Name, "Kris", 500L, "trader2")));
        var theirs = safety.GetEngine().Trades("trader2", 5).Single();
        Assert.That((theirs.Got.Single(), theirs.ZenGave), Is.EqualTo(("Kris", 500L)));
    }

    /// <summary>
    /// The state for the web game names the locked slots and what needs the second password.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask StateNamesTheLockedSlotsAsync()
    {
        var context = this.Context();
        var player = await this.PlayerAsync(context, "owner", "4321").ConfigureAwait(false);
        var safety = AccountSafetyPlugIn.Get(context)!;
        var item = CreateItem();
        await player.Inventory!.AddItemAsync(BagSlot, item).ConfigureAwait(false);
        Assert.That(await safety.LockItemAsync(player, false, BagSlot).ConfigureAwait(false), Is.EqualTo(ItemLockResult.Locked));
        Assert.That(await safety.UnlockItemAsync(player, false, BagSlot, "1111").ConfigureAwait(false), Is.Null, "a wrong second password");
        Assert.That(await safety.UnlockItemAsync(player, false, BagSlot, "4321").ConfigureAwait(false), Is.EqualTo(ItemLockResult.UnlockStarted));

        using var json = JsonDocument.Parse(safety.WriteState(player, new { type = "prompt", action = "trade" }));
        var root = json.RootElement;
        Assert.That(root.GetProperty("hasPin").GetBoolean(), Is.True);
        Assert.That(root.GetProperty("entered").GetBoolean(), Is.False);
        Assert.That(root.GetProperty("required").GetProperty("trade").GetBoolean(), Is.True);
        var locked = root.GetProperty("locks")[0];
        Assert.That((locked.GetProperty("storage").GetString(), locked.GetProperty("slot").GetInt32(), locked.GetProperty("name").GetString()), Is.EqualTo(("inventory", (int)BagSlot, "Kris")));
        Assert.That(locked.TryGetProperty("unlockAt", out _), Is.True);
        Assert.That(root.GetProperty("event").GetProperty("action").GetString(), Is.EqualTo("trade"));
    }

    private static HNMO.Persistence.BasicModel.Item CreateItem()
    {
        var definitionMock = new Mock<ItemDefinition>();
        definitionMock.SetupAllProperties();
        definitionMock.Setup(d => d.BasePowerUpAttributes).Returns(new List<ItemBasePowerUpDefinition>());
        definitionMock.Setup(d => d.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        definitionMock.Setup(d => d.DropItems).Returns(new List<ItemDropItemGroup>());
        var definition = definitionMock.Object;
        definition.Width = 1;
        definition.Height = 1;
        definition.Durability = 10;
        definition.Name = "Kris";
        definition.IsDroppable = true;
        definition.IsSellableToNpc = true;
        definition.IsTradable = true;
        definition.IsPersonalStoreSellable = true;
        definition.IsStorable = true;
        return new HNMO.Persistence.BasicModel.Item { Id = Guid.NewGuid(), Definition = definition, Durability = 1 };
    }

    private AccountSafetyEngine Engine(AccountSafetyConfiguration configuration) => new(configuration, new AccountSafetyStore(this._file));

    private void Save(AccountSafetyEngine engine)
    {
        lock (engine.Store.Lock)
        {
            engine.Store.SaveIfChanged();
        }
    }

    private IGameContext Context()
    {
        var configuration = new PlugInConfiguration
        {
            TypeId = typeof(AccountSafetyPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new AccountSafetyConfiguration { FileName = this._file, MaxWrongTries = 5 }),
        };
        return GameContextTestHelper.CreateGameContext([configuration]);
    }

    private async ValueTask<Player> PlayerAsync(IGameContext context, string login, string? pin = null)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        var account = new HNMO.Persistence.BasicModel.Account { LoginName = login, VaultPassword = pin is null ? string.Empty : AccountSafetyEngine.HashPin(pin) };
        player.Account = account;
        player.SelectedCharacter!.Name = login + "Dk";
        return player;
    }
}
