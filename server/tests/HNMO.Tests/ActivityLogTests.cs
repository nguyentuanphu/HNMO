// <copyright file="ActivityLogTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using System.Threading;
using Moq;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.Views.NPC;
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

    /// <summary>Only matching origin tokens within retention contribute to item history.</summary>
    [Test]
    public void ItemHistoryUsesExactOriginSerialWithinRetention()
    {
        var store = ActivityLogStore.Open(this._file);
        var today = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
        var serial = Guid.NewGuid();
        var serialText = serial.ToString("N");
        store.Append(new() { TimestampUtc = today, Kind = ActivityKind.Trade, ItemIds = serialText });
        store.Append(new() { TimestampUtc = today, Kind = ActivityKind.Grant, ItemSerials = serialText + "0" });
        store.Append(new() { TimestampUtc = today.AddMinutes(-1), Kind = ActivityKind.Pickup, ItemSerials = serialText });
        store.Append(new() { TimestampUtc = today.AddMinutes(-2), Kind = ActivityKind.ChaosMix, ItemSerials = $"{Guid.NewGuid():N},{serialText}" });
        store.Append(new() { TimestampUtc = today.AddDays(-11), Kind = ActivityKind.Loot, ItemSerials = serialText });

        var result = store.QueryItemHistory(serial, today.AddDays(-10), 2);
        Assert.That(result.Select(entry => entry.Kind), Is.EqualTo(new[] { ActivityKind.Pickup, ActivityKind.ChaosMix }));
        Assert.That(store.QueryItemHistory(serial, today.AddDays(-10), 1).Single().Kind, Is.EqualTo(ActivityKind.Pickup));
        Assert.That(store.QueryItemHistory(Guid.Empty, today.AddDays(-10), 2), Is.Empty);
    }

    /// <summary>Retained fights are selected by the mode recorded at defeat.</summary>
    [Test]
    public void PvpHistoryReadsOnlyTheExactCombatMode()
    {
        var store = ActivityLogStore.Open(this._file);
        var now = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
        store.Append(new() { TimestampUtc = now, Kind = ActivityKind.PvpKill, PvpMode = ActivityPvpMode.Field });
        store.Append(new() { TimestampUtc = now, Kind = ActivityKind.PvpKill, PvpMode = ActivityPvpMode.Arena });
        store.Append(new() { TimestampUtc = now.AddDays(-11), Kind = ActivityKind.PvpKill, PvpMode = ActivityPvpMode.Arena });
        store.Append(new() { TimestampUtc = now, Kind = ActivityKind.PvpKill });
        store.Append(new() { TimestampUtc = now, Kind = ActivityKind.Administration, PvpMode = ActivityPvpMode.Arena });

        Assert.That(store.QueryPvpKills(ActivityPvpMode.Arena, now.AddDays(-10), 4).Single().TimestampUtc, Is.EqualTo(now));
        Assert.That(store.QueryPvpKills(ActivityPvpMode.Siege, now.AddDays(-10), 4), Is.Empty);
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

    /// <summary>Gameplay can fill the bounded buffer while persistence is blocked, and the full burst uses batched writes.</summary>
    [Test]
    public void QueuedBurstUsesOneBackendCallPerBatchAndDoesNotBlockProducers()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var mock = new Mock<IHnmoPlayerStateRowBackend>();
        using var allowWrite = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var batches = 0;
        mock.Setup(value => value.LoadAll(ActivityLogStore.FeatureKey)).Returns(() => backend.LoadAll(ActivityLogStore.FeatureKey));
        mock.Setup(value => value.UpsertBatch(It.IsAny<IReadOnlyList<HnmoFeatureRow>>(), It.IsAny<IReadOnlyList<(string, string)>>()))
            .Callback<IReadOnlyList<HnmoFeatureRow>, IReadOnlyList<(string, string)>>((rows, deletes) =>
            {
                Assert.That(rows, Has.Count.EqualTo(ActivityLogStore.BatchSize));
                if (Interlocked.Increment(ref batches) == 1)
                {
                    started.SetResult();
                    Assert.That(allowWrite.Wait(TimeSpan.FromSeconds(15)), Is.True);
                }

                backend.UpsertBatch(rows, deletes);
            });
        var path = HnmoStateStore.ResolveStateFilePath(this._file)!;
        HnmoPlayerStateRowStoreRegistry.RegisterBackend(path, mock.Object);
        // Keep this isolated writer out of unrelated game contexts' periodic flushes.
        var store = new ActivityLogStore(path);
        var utc = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
        var entry = new ActivityLogEntry { TimestampUtc = utc, Kind = ActivityKind.Loot };
        for (var index = 0; index < ActivityLogStore.BatchSize; index++)
        {
            Assert.That(store.Enqueue(entry, retentionDays: 9), Is.True);
        }

        Assert.That(batches, Is.Zero);
        var writer = Task.Factory.StartNew(store.FlushPending, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.That(Task.WaitAny([started.Task, writer], TimeSpan.FromSeconds(10)), Is.EqualTo(0), "The writer must reach the blocked backend.");
            var producer = Task.Factory.StartNew(() =>
            {
                for (var index = ActivityLogStore.BatchSize; index < ActivityLogStore.QueueCapacity; index++)
                {
                    Assert.That(store.Enqueue(entry, retentionDays: 9), Is.True);
                }

                Assert.That(store.Enqueue(entry, retentionDays: 9), Is.False);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(producer.Wait(TimeSpan.FromSeconds(10)), Is.True, "The producer must finish while the backend is blocked.");
        }
        finally
        {
            allowWrite.Set();
        }

        Assert.That(writer.Wait(TimeSpan.FromSeconds(15)), Is.True);
        Assert.That(writer.Result, Is.EqualTo(ActivityLogStore.QueueCapacity));
        Assert.That(batches, Is.EqualTo(ActivityLogStore.QueueCapacity / ActivityLogStore.BatchSize));
        var saved = backend.LoadAll(ActivityLogStore.FeatureKey);
        Assert.That(saved, Has.Count.EqualTo(ActivityLogStore.QueueCapacity));
        Assert.That(saved.Select(row => row.ExpiresAtUtc).Distinct().Single(), Is.EqualTo(utc.AddDays(9)));
    }

    /// <summary>Retries after an uncertain upsert keep the same durable event identifiers.</summary>
    [Test]
    public void RetryingAnUnacknowledgedBatchDoesNotDuplicateRows()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var mock = new Mock<IHnmoPlayerStateRowBackend>();
        var attempts = 0;
        mock.Setup(value => value.LoadAll(ActivityLogStore.FeatureKey)).Returns(() => backend.LoadAll(ActivityLogStore.FeatureKey));
        mock.Setup(value => value.UpsertBatch(It.IsAny<IReadOnlyList<HnmoFeatureRow>>(), It.IsAny<IReadOnlyList<(string, string)>>()))
            .Callback<IReadOnlyList<HnmoFeatureRow>, IReadOnlyList<(string, string)>>((rows, deletes) =>
            {
                backend.UpsertBatch(rows, deletes);
                if (++attempts == 1)
                {
                    throw new IOException("Test acknowledgement failure.");
                }
            });
        var path = HnmoStateStore.ResolveStateFilePath(this._file)!;
        HnmoPlayerStateRowStoreRegistry.RegisterBackend(path, mock.Object);
        var store = new ActivityLogStore(path);
        Assert.That(store.Enqueue(new() { Kind = ActivityKind.Grant }, retentionDays: 10), Is.True);
        Assert.That(() => store.FlushPending(), Throws.TypeOf<IOException>());
        var persisted = backend.LoadAll(ActivityLogStore.FeatureKey).Single().ScopeId;
        Assert.That(store.Query().Single().Id, Is.EqualTo(persisted));
        Assert.That(store.FlushPending(), Is.EqualTo(1));
        Assert.That(backend.LoadAll(ActivityLogStore.FeatureKey).Single().ScopeId, Is.EqualTo(persisted));
        Assert.That(store.Query(), Has.Count.EqualTo(1));
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
        Assert.That(bless.Serial, Is.Not.Null.And.Not.EqualTo(bless.Id));
        var serial = bless.Serial!.Value.ToString("N");
        Assert.That(entries.Single(e => e.Kind == ActivityKind.Drop).ItemSerials, Is.EqualTo(serial));
        var pickup = entries.Single(e => e.Kind == ActivityKind.Pickup);
        Assert.That(pickup.ItemSerials, Is.EqualTo(serial));
        Assert.That(pickup.PickupQuantity, Is.EqualTo(1));
        Assert.That(pickup.ItemIds, Is.Empty);
    }

    /// <summary>Craft, grant, consume, PvP and upgrade events use stable serials and immutable before and after values.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async ValueTask NewGameplayEventsPreserveSerialsAndUpgradeSnapshotsAsync()
    {
        var context = GameContextTestHelper.CreateGameContext([]);
        var player = await CreatePlayerAsync(context, "crafter").ConfigureAwait(false);
        var other = await CreatePlayerAsync(context, "opponent").ConfigureAwait(false);
        var activity = new ActivityLogPlugIn
        {
            Configuration = new() { FileName = this._file, OnlyValuableDropsAndPickups = false },
        };
        var material = CreateItem("Test material", 0, false);
        var equipment = CreateItem("Test equipment", 4, false);
        activity.RecordLoot(player, material, "Fixture monster");
        activity.RecordGrant(player, equipment, "Fixture reward");
        var target = ActivityItemState.Capture(equipment);
        var consumed = ActivityItemState.Capture(material)!;
        equipment.Level = 5;
        activity.RecordConsumption(player, consumed, null, target, equipment);
        var mixInput = ActivityLogPlugIn.CaptureItems([equipment]);
        equipment.Level = 6;
        activity.RecordCraft(player, CraftingResult.Success, "Fixture mix", mixInput, [equipment]);
        activity.RecordPvpKill(player, other);

        var store = ActivityLogStore.Open(this._file);
        Assert.That(store.FlushPending(), Is.EqualTo(7));
        ActivityLogStore.ResetForTests();
        var events = ActivityLogStore.Open(this._file).Query();
        Assert.That(events.Select(entry => entry.Kind), Is.EquivalentTo(new[]
        {
            ActivityKind.Loot, ActivityKind.Grant, ActivityKind.Consume, ActivityKind.Upgrade,
            ActivityKind.ChaosMix, ActivityKind.Upgrade, ActivityKind.PvpKill,
        }));
        Assert.That(events.Single(entry => entry.Kind == ActivityKind.Grant).AfterItems!.Single().Level, Is.EqualTo(4));
        Assert.That(events.Single(entry => entry.Kind == ActivityKind.Loot).AfterItems!.Single().Serial, Is.EqualTo(material.Serial));
        Assert.That(events.Single(entry => entry.Kind == ActivityKind.PvpKill).PvpMode, Is.EqualTo(ActivityPvpMode.Field));
        Assert.That(events.Single(entry => entry.Kind == ActivityKind.Consume).AfterItems, Is.Empty);
        var upgrades = events.Where(entry => entry.Kind == ActivityKind.Upgrade).OrderBy(entry => entry.BeforeItems![0].Level).ToArray();
        Assert.That(upgrades.Select(entry => entry.BeforeItems![0].Level), Is.EqualTo(new byte[] { 4, 5 }));
        Assert.That(upgrades.Select(entry => entry.AfterItems![0].Level), Is.EqualTo(new byte[] { 5, 6 }));
        Assert.That(upgrades.Select(entry => entry.ItemSerials).Distinct().Single(), Is.EqualTo(equipment.Serial!.Value.ToString("N")));
        Assert.That(equipment.Serial, Is.Not.EqualTo(equipment.Id));
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
