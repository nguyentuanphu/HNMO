// <copyright file="ActivityLogTests.cs" company="HNMO">
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
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.Views.Trade;
using HNMO.Pathfinding;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Tests <see cref="ActivityLogPlugIn"/> and <see cref="ActivityLogStore"/> (ROADMAP 14.9).
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ActivityLogTests
{
    private string _file = null!;

    /// <summary>Sets up an isolated row store path per test.</summary>
    [SetUp]
    public void SetUp()
    {
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        ActivityLogStore.ResetForTests();
        this._file = Path.Combine(Path.GetTempPath(), $"hnmo-activity-{Guid.NewGuid():N}.json");
    }

    /// <summary>Cleans up temporary test state.</summary>
    [TearDown]
    public void TearDown()
    {
        ActivityLogStore.ResetForTests();
        HnmoPlayerStateRowStoreRegistry.ResetForTests();
        if (File.Exists(this._file))
        {
            File.Delete(this._file);
        }
    }

    /// <summary>
    /// Verifies row-level persistence, reload across store reset, filtering (account/character/item/type/date range),
    /// Item Market external logging, and expired row archival.
    /// </summary>
    [Test]
    public void StorePersistsQueriesAndArchivesRows()
    {
        var store = ActivityLogStore.Open(this._file);
        var now = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
        var targetGuid = Guid.NewGuid().ToString("N");

        store.Append(
            new ActivityLogEntry
            {
                TimestampUtc = now.AddDays(-10),
                Kind = ActivityKind.Drop,
                Account = "alice",
                Character = "AliceBM",
                ItemSummary = "Dragon Armor +9",
                ItemIds = Guid.NewGuid().ToString("N"),
                Details = "Map=Lorencia",
            },
            retentionDays: 5);

        store.Append(
            new ActivityLogEntry
            {
                TimestampUtc = now.AddHours(-2),
                Kind = ActivityKind.Trade,
                Account = "alice",
                Character = "AliceBM",
                PartnerAccount = "bob",
                PartnerCharacter = "BobSM",
                ItemSummary = "Excellent Kris +7",
                ItemIds = targetGuid,
                ZenDelta = 50000,
                Details = "ZenGave=0;ZenGot=50000",
            },
            retentionDays: 30);

        ActivityLogPlugIn.RecordExternal(
            new ActivityLogEntry
            {
                TimestampUtc = now,
                Kind = ActivityKind.ItemMarket,
                Account = "charlie",
                Character = "CharlieElf",
                PartnerAccount = "alice",
                PartnerCharacter = "AliceBM",
                ItemSummary = "Jewel of Bless x10",
                Details = "MarketId=M42",
            },
            this._file,
            retentionDays: 30);

        // Simulate process restart against the same persisted row backend.
        ActivityLogStore.ResetForTests();
        var reloaded = ActivityLogStore.Open(this._file);

        Assert.That(reloaded.Query().Count, Is.EqualTo(3));
        Assert.That(reloaded.Query(new ActivityLogQuery { Account = "bob" }).Single().Kind, Is.EqualTo(ActivityKind.Trade));
        Assert.That(reloaded.Query(new ActivityLogQuery { Character = "Charlie" }).Single().Kind, Is.EqualTo(ActivityKind.ItemMarket));
        Assert.That(reloaded.Query(new ActivityLogQuery { Item = targetGuid[..8] }).Single().Account, Is.EqualTo("alice"));
        Assert.That(reloaded.Query(new ActivityLogQuery { Kind = ActivityKind.ItemMarket }).Single().Details, Is.EqualTo("MarketId=M42"));
        Assert.That(reloaded.Query(new ActivityLogQuery { FromUtc = now.AddHours(-3), ToUtc = now }).Count, Is.EqualTo(2));

        var archived = reloaded.ArchiveExpired(now);
        Assert.That(archived, Is.EqualTo(1));
        Assert.That(reloaded.Query().Count, Is.EqualTo(2));
    }

    /// <summary>Verifies valuable-item classification for drop/pickup filtering.</summary>
    [Test]
    public void ValuableItemFilterMatchesExpectedTiers()
    {
        var common = CreateItem("Small Healing Potion", level: 0, isExcellent: false);
        var upgraded = CreateItem("Kris", level: 7, isExcellent: false);
        var jewel = CreateItem("Jewel of Soul", level: 0, isExcellent: false);

        Assert.That(ActivityLogPlugIn.IsValuableItem(common, minLevel: 7), Is.False);
        Assert.That(ActivityLogPlugIn.IsValuableItem(upgraded, minLevel: 7), Is.True);
        Assert.That(ActivityLogPlugIn.IsValuableItem(jewel, minLevel: 7), Is.True);
    }

    /// <summary>Verifies live trade, drop, pickup, and personal store actions record into the activity log.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask GameActionsRecordTradeDropPickupAndPersonalStoreAsync()
    {
        var pluginConfig = new PlugInConfiguration
        {
            TypeId = typeof(ActivityLogPlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new ActivityLogConfiguration
            {
                Enabled = true,
                OnlyValuableDropsAndPickups = true,
                MinValuableDropLevel = 7,
                FileName = this._file,
            }),
        };

        var context = GameContextTestHelper.CreateGameContext([pluginConfig]);
        var player1 = await CreatePlayerAsync(context, "acc1").ConfigureAwait(false);
        var player2 = await CreatePlayerAsync(context, "acc2").ConfigureAwait(false);
        player1.Attributes![HNMO.GameLogic.Attributes.Stats.Level] = 10;
        player2.Attributes![HNMO.GameLogic.Attributes.Stats.Level] = 10;
        player2.Position = new Point((byte)(player1.Position.X + 1), player1.Position.Y);

        // 1. Direct trade
        await new TradeRequestAction().RequestTradeAsync(player1, player2).ConfigureAwait(false);
        await new TradeAcceptAction().HandleTradeAcceptAsync(player2, true).ConfigureAwait(false);
        var tradedItem = CreateItem("Legendary Staff", level: 9, isExcellent: false);
        await player1.TemporaryStorage!.AddItemAsync(0, tradedItem).ConfigureAwait(false);
        player2.Money = 10000;
        player2.TradingMoney = 2500;
        var tradeButtons = new TradeButtonAction();
        await tradeButtons.TradeButtonChangedAsync(player1, TradeButtonState.Checked).ConfigureAwait(false);
        await tradeButtons.TradeButtonChangedAsync(player2, TradeButtonState.Checked).ConfigureAwait(false);

        // 2. Drop and pickup of valuable item
        var bless = CreateItem("Jewel of Bless", level: 0, isExcellent: false);
        await player2.Inventory!.AddItemAsync(20, bless).ConfigureAwait(false);
        await new DropItemAction().DropItemAsync(player2, 20, player2.Position).ConfigureAwait(false);
        var drop = player2.CurrentMap!.GetDropsInRange(player2.Position, 5).OfType<DroppedItem>().Single();
        await new PickupItemAction().PickupItemAsync(player2, drop.Id).ConfigureAwait(false);

        // 3. Personal store purchase
        var storeDef = new HNMO.Persistence.BasicModel.ItemDefinition
        {
            Name = "Dark Knight Blade",
            Width = 1,
            Height = 1,
            Durability = 10,
            MaximumItemLevel = 15,
            IsDroppable = true,
            IsSellableToNpc = true,
            IsTradable = true,
            IsPersonalStoreSellable = true,
            IsStorable = true,
        };
        var storeItem = new HNMO.Persistence.BasicModel.Item { Id = Guid.NewGuid(), Definition = storeDef, Level = 11, Durability = 1, StorePrice = 1500 };
        player2.Money = 50000;
        Assert.That(await player1.ShopStorage!.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, storeItem).ConfigureAwait(false), Is.True);
        player1.ShopStorage!.StoreOpen = true;
        await new BuyRequestAction().BuyItemAsync(player2, player1, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        var entries = ActivityLogStore.Open(this._file).Query();
        var diag = string.Join(" | ", entries.Select(e => $"{e.Kind}:{e.Account}:{e.ItemSummary}:{e.ZenDelta}"));
        Assert.That(player2.Inventory!.Items, Does.Contain(storeItem), $"StoreOpen={player1.ShopStorage!.StoreOpen}, Price={storeItem.StorePrice}, Entries={diag}");
        Assert.That(entries.Any(e => e.Kind == ActivityKind.Trade && e.ItemSummary.Contains("Legendary Staff", StringComparison.Ordinal)), Is.True, diag);
        Assert.That(entries.Any(e => e.Kind == ActivityKind.Drop && e.ItemSummary.Contains("Jewel of Bless", StringComparison.Ordinal)), Is.True, diag);
        Assert.That(entries.Any(e => e.Kind == ActivityKind.Pickup && e.ItemSummary.Contains("Jewel of Bless", StringComparison.Ordinal)), Is.True, diag);
        Assert.That(entries.Any(e => e.Kind == ActivityKind.PersonalStore && e.ZenDelta == 1500), Is.True, diag);
    }

    private static HNMO.Persistence.BasicModel.Item CreateItem(string name, byte level, bool isExcellent)
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
        definition.Name = name;
        definition.IsDroppable = true;
        definition.IsSellableToNpc = true;
        definition.IsTradable = true;
        definition.IsPersonalStoreSellable = true;
        definition.IsStorable = true;
        definition.MaximumItemLevel = 15;
        return new HNMO.Persistence.BasicModel.Item
        {
            Id = Guid.NewGuid(),
            Definition = definition,
            Level = level,
            Durability = 1,
        };
    }

    private static async ValueTask<Player> CreatePlayerAsync(IGameContext context, string login)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.Account = new HNMO.Persistence.BasicModel.Account { LoginName = login, VaultPassword = string.Empty };
        player.SelectedCharacter!.Name = login + "Hero";
        return player;
    }
}
