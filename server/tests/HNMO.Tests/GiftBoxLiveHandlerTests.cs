// <copyright file="GiftBoxLiveHandlerTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.IO;
using System.Text.Json;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.GameLogic.Views.Inventory;
using HNMO.Persistence;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Live mailbox actions under exclusive persistence lock, inventory cache inclusion, activity hooks, and uncertain-save disconnect.</summary>
[TestFixture]
public class GiftBoxLiveHandlerTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 6, 0, 0, DateTimeKind.Utc);

    /// <summary>Entering the world observes recent activity without regressing newer timestamps.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ActivityPlugInObservesWorldEntryAndDepartureAsync()
    {
        var backend = new InMemoryHnmoPlayerStateRowBackend();
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var accountId = Guid.NewGuid();
        var account = new HNMO.Persistence.BasicModel.Account { Id = accountId, LoginName = "AnnLive" };
        player.Account = account;

        var time = new FixedTimeProvider(Now);
        var plugIn = new GiftBoxActivityPlugIn(backend, time);
        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.EnteredWorld).ConfigureAwait(false);
        Assert.That(new GiftBoxActivity(backend).Read()[accountId], Is.EqualTo(Now));

        time.UtcNow = Now.AddMinutes(5);
        await plugIn.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.Disconnected).ConfigureAwait(false);
        Assert.That(new GiftBoxActivity(backend).Read()[accountId], Is.EqualTo(Now.AddMinutes(5)));
    }

    /// <summary>Live collection updates bag storage without duplicating entities, invokes inventory/zen views, and logs activity.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LiveCollectionCreditedAfterConfirmedSaveRefreshesInventoryAndLogsActivityAsync()
    {
        var rows = new InMemoryHnmoPlayerStateRowBackend();
        var (player, account, character) = await CreateLivePlayerAsync("AnnLive").ConfigureAwait(false);

        var dispatch = new GiftBoxDispatch(rows);
        var letterId = Guid.NewGuid();
        dispatch.Send(
            letterId,
            "host",
            "live delivery check",
            [account.GetId()],
            "Event",
            "Su kien",
            "Enjoy",
            "Chuc vui",
            new GiftCodeReward
            {
                Coins = 50,
                CoinType = CashShopCoinType.WCoinC,
                Zen = 1000,
                Items = [new() { Group = 0, Number = 0, Count = 1, Level = 3 }],
            },
            Now);

        var activities = new List<ActivityLogEntry>();
        var actions = new GiftBoxGameActions(rows, new FixedTimeProvider(Now), activities.Add);
        var json = await actions.HandleAsync(player, new GiftBoxRequest("collect", 0, letterId, character.GetId(), "vi")).ConfigureAwait(false);

        Assert.That(json, Is.Not.Null);
        using var doc = JsonDocument.Parse(json!);
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("Applied"));
        Assert.That(account.WCoinC, Is.EqualTo(60));
        Assert.That(character.Inventory!.Money, Is.EqualTo(1500));
        Assert.That(character.Inventory.Items, Has.Count.EqualTo(1));
        Assert.That(player.Inventory!.Items.Count(), Is.EqualTo(1));
        Mock.Get(player.ViewPlugIns.GetPlugIn<IItemAppearPlugIn>()!).Verify(v => v.ItemAppearAsync(It.IsAny<Item>()), Times.Once);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateMoneyPlugIn>()!).Verify(v => v.UpdateMoneyAsync(), Times.Once);
        Assert.That(activities, Has.Count.EqualTo(1));
        Assert.That(activities[0].Account, Is.EqualTo("AnnLive"));
        Assert.That(activities[0].Character, Is.EqualTo("AnnLiveDk"));
    }

    /// <summary>A season whose cash shop is off gives a letter's zen and items, but not its coins (owner 2026-11-28, classic play).</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task NoCoinsFromLettersWhileTheCashShopIsOffAsync()
    {
        var rows = new InMemoryHnmoPlayerStateRowBackend();
        var (player, account, character) = await CreateLivePlayerAsync("AnnClassic", cashShopOn: false).ConfigureAwait(false);
        var dispatch = new GiftBoxDispatch(rows);
        var letterId = Guid.NewGuid();
        dispatch.Send(letterId, "host", "classic check", [account.GetId()], "Event", "Su kien", "Enjoy", "Chuc vui", new GiftCodeReward { Coins = 50, CoinType = CashShopCoinType.WCoinC, Zen = 1000 }, Now);

        var actions = new GiftBoxGameActions(rows, new FixedTimeProvider(Now));
        var json = await actions.HandleAsync(player, new GiftBoxRequest("collect", 0, letterId, character.GetId(), "en")).ConfigureAwait(false);

        Assert.That(json, Is.Not.Null);
        Assert.That(account.WCoinC, Is.EqualTo(10));
        Assert.That(character.Inventory!.Money, Is.EqualTo(1500));
    }

    /// <summary>An uncertain live save disconnects the player before unlocking and never reveals goods.</summary>    /// <returns>The test task.</returns>
    [Test]
    public async Task UncertainSaveDisconnectsPlayerBeforeReleasingLockAsync()
    {
        var rows = new InMemoryHnmoPlayerStateRowBackend();
        var (player, account, character) = await CreateLivePlayerAsync("AnnUncertain").ConfigureAwait(false);

        var dispatch = new GiftBoxDispatch(rows);
        var letterId = Guid.NewGuid();
        dispatch.Send(
            letterId,
            "host",
            "uncertain check",
            [account.GetId()],
            "Gift",
            "Qua",
            "Body",
            "Noi dung",
            new GiftCodeReward { Coins = 10, CoinType = CashShopCoinType.WCoinC },
            Now);

        var actions = new GiftBoxGameActions(rows, new FixedTimeProvider(Now));
        var json = await actions.HandleAsync(
            player,
            new GiftBoxRequest("collect", 0, letterId, character.GetId(), "en"),
            () => ValueTask.FromException<bool>(new IOException("connection dropped after staging"))).ConfigureAwait(false);

        Assert.That(json, Is.Null);
        Assert.That(player.Disconnected, Is.True);
        Assert.That(GiftBoxCollection.HasUnconfirmed(player.PersistenceContext), Is.True);
        Mock.Get(player.ViewPlugIns.GetPlugIn<IItemAppearPlugIn>()!).Verify(v => v.ItemAppearAsync(It.IsAny<Item>()), Times.Never);
    }

    private static async ValueTask<(LiveTestPlayer Player, Account Account, Character Character)> CreateLivePlayerAsync(string login, bool cashShopOn = true)
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var setup = provider.CreateNewContext();
        var config = setup.CreateNew<GameConfiguration>();
        config.MaximumInventoryMoney = 2_000_000_000;
        var map = setup.CreateNew<GameMapDefinition>();
        map.Number = 0;
        map.TerrainData = new byte[ushort.MaxValue + 3];
        config.Maps.Add(map);
        var sword = setup.CreateNew<ItemDefinition>();
        sword.Group = 0;
        sword.Number = 0;
        sword.Width = 1;
        sword.Height = 1;
        sword.MaximumItemLevel = 15;
        sword.Durability = 100;
        config.Items.Add(sword);

        var charClass = setup.CreateNew<CharacterClass>();
        charClass.Number = 0;
        charClass.Name = "Dark Knight";
        foreach (var stat in new[]
        {
            Stats.Level, Stats.MasterLevel, Stats.BaseStrength, Stats.BaseAgility,
            Stats.BaseVitality, Stats.BaseEnergy, Stats.CurrentHealth, Stats.CurrentMana,
            Stats.CurrentShield, Stats.Resets, Stats.PointsPerReset,
        })
        {
            var def = setup.CreateNew<AttributeDefinition>(stat.Id, stat.Designation, stat.Description);
            config.Attributes.Add(def);
            charClass.StatAttributes.Add(setup.CreateNew<StatAttributeDefinition>(def, stat == Stats.BaseStrength ? 28 : 10, true));
        }

        config.CharacterClasses.Add(charClass);
        await setup.SaveChangesAsync().ConfigureAwait(false);

        using (var seed = provider.CreateNewPlayerContext(config))
        {
            var seededAccount = seed.CreateNew<Account>();
            seededAccount.LoginName = login;
            seededAccount.WCoinC = 10;
            var seededChar = seed.CreateNew<Character>();
            seededChar.Name = login + "Dk";
            seededChar.CharacterClass = charClass;
            seededChar.CurrentMap = map;
            seededChar.Inventory = seed.CreateNew<ItemStorage>();
            seededChar.Inventory.Money = 500;
            seededAccount.Characters.Add(seededChar);
            await seed.SaveChangesAsync().ConfigureAwait(false);
        }

        var mapInitializer = new MapInitializer(config, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var gameContext = new GameContext(config, provider, mapInitializer, NullLoggerFactory.Instance, new HNMO.PlugIns.PlugInManager(null, NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;
        if (cashShopOn)
        {
            gameContext.FeaturePlugIns.AddPlugIn(new HNMO.GameLogic.CashShop.CashShopFeaturePlugIn(), true);
        }

        var player = new LiveTestPlayer(gameContext);
        var account = (await player.PersistenceContext.GetAccountByLoginNameAsync(login).ConfigureAwait(false))!;
        var character = account.Characters.Single();
        player.Account = account;
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);
        await player.SetSelectedCharacterAsync(character).ConfigureAwait(false);
        return (player, account, character);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => new(this.UtcNow, TimeSpan.Zero);
    }

    private sealed class LiveTestPlayer(IGameContext gameContext)
        : Player(gameContext)
    {
        public bool Disconnected { get; private set; }

        protected override HNMO.PlugIns.ICustomPlugInContainer<HNMO.GameLogic.Views.IViewPlugIn> CreateViewPlugInContainer()
            => new MockViewPlugInContainer();

        protected override ValueTask InternalDisconnectAsync()
        {
            this.Disconnected = true;
            return base.InternalDisconnectAsync();
        }
    }
}
