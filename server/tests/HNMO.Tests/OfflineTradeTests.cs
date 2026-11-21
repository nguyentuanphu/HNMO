// <copyright file="OfflineTradeTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.Text.Json;
using HNMO.DataModel;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlayerActions.PlayerStore;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.OfflineTrade;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Unit and integration tests for <see cref="OfflineTradeEngine"/> and <see cref="OfflineTradePlugIn"/>.
/// </summary>
[TestFixture]
public class OfflineTradeTests
{
    /// <summary>Verifies <see cref="OfflineTradeEngine.EvaluateStart"/> covers all rules and limits.</summary>
    [Test]
    public void EngineValidatesStoreSafeZoneLevelMapCoinsAndZenFee()
    {
        var cfg = new OfflineTradeConfiguration
        {
            SafeZoneOnly = true,
            AllowCoinCurrencies = false,
            MaxDurationHours = 24,
            OpenFeeZen = 10000,
            StopWhenSoldOut = true,
            MinimumLevel = 50,
            AllowedMapNumbers = [0, 2],
        };

        Assert.That(
            OfflineTradeEngine.EvaluateStart(cfg, false, true, true, false, false, true, 1, true, 0, 100, 50000, false).Reason,
            Is.EqualTo(OfflineTradeDenialReason.Disabled));
        Assert.That(
            OfflineTradeEngine.EvaluateStart(cfg, true, true, true, false, false, false, 1, true, 0, 100, 50000, false).Reason,
            Is.EqualTo(OfflineTradeDenialReason.StoreNotOpen));
        Assert.That(
            OfflineTradeEngine.EvaluateStart(cfg, true, true, true, false, false, true, 0, true, 0, 100, 50000, false).Reason,
            Is.EqualTo(OfflineTradeDenialReason.StoreEmpty));
        Assert.That(
            OfflineTradeEngine.EvaluateStart(cfg, true, true, true, false, false, true, 1, false, 0, 100, 50000, false).Reason,
            Is.EqualTo(OfflineTradeDenialReason.NotInSafeZone));
        Assert.That(
            OfflineTradeEngine.EvaluateStart(cfg, true, true, true, false, false, true, 1, true, 3, 100, 50000, false).Reason,
            Is.EqualTo(OfflineTradeDenialReason.MapNotAllowed));
        Assert.That(
            OfflineTradeEngine.EvaluateStart(cfg, true, true, true, false, false, true, 1, true, 0, 49, 50000, false).Reason,
            Is.EqualTo(OfflineTradeDenialReason.LevelTooLow));
        Assert.That(
            OfflineTradeEngine.EvaluateStart(cfg, true, true, true, false, false, true, 1, true, 0, 100, 50000, true).Reason,
            Is.EqualTo(OfflineTradeDenialReason.CoinCurrenciesNotAllowed));
        Assert.That(
            OfflineTradeEngine.EvaluateStart(cfg, true, true, true, false, false, true, 1, true, 0, 100, 9999, false).Reason,
            Is.EqualTo(OfflineTradeDenialReason.NotEnoughZen));

        cfg.AllowCoinCurrencies = true;
        var ok = OfflineTradeEngine.EvaluateStart(cfg, true, true, true, false, false, true, 2, true, 0, 100, 20000, true);
        Assert.That(ok.Allowed, Is.True);
        Assert.That(ok.ZenFee, Is.EqualTo(10000));
        Assert.That(ok.MaxDurationHours, Is.EqualTo(24));

        var start = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(OfflineTradeEngine.IsExpired(start, start.AddHours(23), 24), Is.False);
        Assert.That(OfflineTradeEngine.IsExpired(start, start.AddHours(24), 24), Is.True);
        Assert.That(OfflineTradeEngine.IsExpired(start, start.AddHours(500), 0), Is.False);
    }

    /// <summary>Verifies disabled <c>/offtrade</c> refuses in EN/VI and enabled validates safe zone.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async ValueTask CommandRefusesWhenDisabledAndRequiresSafeZoneWhenActiveAsync()
    {
        var disabledCfg = new PlugInConfiguration
        {
            TypeId = typeof(OfflineTradePlugIn).GUID,
            IsActive = false,
        };
        var offCtx = GameContextTestHelper.CreateGameContext([disabledCfg]);
        var pVi = await PlayerTestHelper.CreatePlayerAsync(offCtx).ConfigureAwait(false);
        pVi.Culture = CultureInfo.GetCultureInfo("vi-VN");
        var cmd = new OfflineTradeChatCommandPlugIn();
        await cmd.HandleCommandAsync(pVi, "/offtrade").ConfigureAwait(false);
        Mock.Get(pVi.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(m => m.Contains("/offtrade") && m.Contains("chưa được bật")), MessageType.BlueNormal), Times.Once);

        var enabledCfg = new PlugInConfiguration
        {
            TypeId = typeof(OfflineTradePlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new OfflineTradeConfiguration { SafeZoneOnly = true, MinimumLevel = 1 }),
        };
        var onCtx = GameContextTestHelper.CreateGameContext([enabledCfg]);
        var seller = await PlayerTestHelper.CreatePlayerAsync(onCtx).ConfigureAwait(false);
        seller.Account!.LoginName = "seller1";
        seller.Attributes![Stats.Level] = 10;

        var storeDef = CreateSellableItemDefinition();
        var item = new HNMO.Persistence.BasicModel.Item { Id = Guid.NewGuid(), Definition = storeDef, Level = 3, Durability = 1, StorePrice = 2000 };
        Assert.That(await seller.ShopStorage!.AddItemAsync(InventoryConstants.FirstStoreItemSlotIndex, item).ConfigureAwait(false), Is.True);
        seller.ShopStorage!.StoreOpen = true;

        // Safezone defaults to false on test map; must warn and refuse.
        await cmd.HandleCommandAsync(seller, "/offtrade").ConfigureAwait(false);
        Mock.Get(seller.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!)
            .Verify(v => v.ShowMessageAsync(It.Is<string>(m => m.Contains("safe zone")), MessageType.BlueNormal), Times.Once);
    }

    /// <summary>Verifies an active off-trade session keeps its shop open without combat AI and stops cleanly when sold out.</summary>
    /// <returns>Async task.</returns>
    [Test]
    public async ValueTask OffTradeSessionKeepsShopOpenWithoutCombatAiAndStopsWhenSoldOutAsync()
    {
        var pluginCfg = new PlugInConfiguration
        {
            TypeId = typeof(OfflineTradePlugIn).GUID,
            IsActive = true,
            CustomConfiguration = JsonSerializer.Serialize(new OfflineTradeConfiguration
            {
                SafeZoneOnly = false,
                MinimumLevel = 1,
                OpenFeeZen = 500,
                StopWhenSoldOut = true,
            }),
        };

        var persistence = new InMemoryPersistenceContextProvider();
        var baseCtx = persistence.CreateNewContext();
        var gameConfig = baseCtx.CreateNew<HNMO.Persistence.BasicModel.GameConfiguration>();
        var mapDef = baseCtx.CreateNew<HNMO.Persistence.BasicModel.GameMapDefinition>();
        mapDef.Number = 0;
        mapDef.Name = "Lorencia";
        mapDef.TerrainData = CreateSafeZoneTerrain();
        gameConfig.Maps.Add(mapDef);
        gameConfig.MaximumPartySize = 5;
        gameConfig.RecoveryInterval = int.MaxValue;
        gameConfig.MaximumInventoryMoney = int.MaxValue;
        gameConfig.ItemDropDuration = TimeSpan.FromMinutes(1);
        gameConfig.MaximumLevel = 400;

        var charClass = baseCtx.CreateNew<HNMO.Persistence.BasicModel.CharacterClass>();
        charClass.Number = 1;
        charClass.Name = "Dark Knight";
        charClass.HomeMap = mapDef;
        gameConfig.CharacterClasses.Add(charClass);

        var storeDef = baseCtx.CreateNew<HNMO.Persistence.BasicModel.ItemDefinition>();
        storeDef.Name = "Dragon Sword";
        storeDef.Width = 1;
        storeDef.Height = 1;
        storeDef.Durability = 10;
        storeDef.MaximumItemLevel = 15;
        storeDef.IsDroppable = true;
        storeDef.IsSellableToNpc = true;
        storeDef.IsTradable = true;
        storeDef.IsPersonalStoreSellable = true;
        storeDef.IsStorable = true;
        gameConfig.Items.Add(storeDef);

        var account = baseCtx.CreateNew<HNMO.Persistence.BasicModel.Account>();
        account.LoginName = "trader1";
        var character = baseCtx.CreateNew<HNMO.Persistence.BasicModel.Character>();
        character.Name = "OffTrader";
        character.CharacterClass = charClass;
        character.CurrentMap = mapDef;
        character.PositionX = 120;
        character.PositionY = 120;
        character.Inventory = baseCtx.CreateNew<HNMO.Persistence.BasicModel.ItemStorage>();
        character.Inventory.Money = 2000;
        character.StoreName = "Cheap Sword";
        character.IsStoreOpened = true;
        var storeItem = baseCtx.CreateNew<HNMO.Persistence.BasicModel.Item>();
        storeItem.Definition = storeDef;
        storeItem.ItemSlot = InventoryConstants.FirstStoreItemSlotIndex;
        storeItem.Durability = 1;
        storeItem.StorePrice = 1500;
        character.Inventory.Items.Add(storeItem);
        account.Characters.Add(character);
        await baseCtx.SaveChangesAsync().ConfigureAwait(false);

        var mapInitializer = new MapInitializer(gameConfig, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var plugInManager = new PlugInManager([pluginCfg], new NullLoggerFactory(), null, null);
        var gameContext = new GameContext(gameConfig, persistence, mapInitializer, new NullLoggerFactory(), plugInManager, NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;

        var offlinePlayer = new OfflinePlayer(gameContext);
        Assert.That(gameContext.OfflinePlayerManager.TryAdd("trader1", offlinePlayer), Is.True);
        Assert.That(await offlinePlayer.InitializeOffTradeAsync("trader1", "OffTrader", 12, true).ConfigureAwait(false), Is.True);

        try
        {
            Assert.That(offlinePlayer.IsOffTrade, Is.True);
            Assert.That(offlinePlayer.ShopStorage!.StoreOpen, Is.True);
            Assert.That(offlinePlayer.SelectedCharacter!.StoreName, Is.EqualTo("Cheap Sword"));
            Assert.That(offlinePlayer.ShopStorage.Items.Count(), Is.EqualTo(1));

            // Now a buyer purchases the last item; seller receives Zen and stops automatically.
            var buyer = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
            buyer.Money = 10000;
            await offlinePlayer.AddObserverAsync(buyer).ConfigureAwait(false);
            await new BuyRequestAction().BuyItemAsync(buyer, offlinePlayer, InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

            Assert.That(buyer.Money, Is.EqualTo(8500));
            Assert.That(buyer.Inventory!.Items.Any(i => i.Definition?.Name == "Dragon Sword"), Is.True);
            Assert.That(gameContext.OfflinePlayerManager.IsActive("trader1"), Is.False);
        }
        finally
        {
            await gameContext.OfflinePlayerManager.StopAsync("trader1").ConfigureAwait(false);
        }
    }

    private static HNMO.Persistence.BasicModel.ItemDefinition CreateSellableItemDefinition() =>
        new()
        {
            Name = "Lorencia Blade",
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

    private static byte[] CreateSafeZoneTerrain()
    {
        var data = new byte[ushort.MaxValue + 3];
        for (var i = 3; i < data.Length; i++)
        {
            data[i] = 0x01; // safezone bit
        }

        return data;
    }
}
