// <copyright file="VipGameActionsTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.Text.Json;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.UltimateAutoPlay;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.GameLogic.Views;
using HNMO.GameServer;
using HNMO.GameServer.RemoteView;
using HNMO.Interfaces;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Persistence;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>Checks the real in-world adapter, live attributes and same-wallet replay boundary.</summary>
[TestFixture]
public class VipGameActionsTests
{
    private Player _player = null!;
    private VipGameActions _actions = null!;
    private VipConfiguration _configuration = null!;
    private VipStateStore _store = null!;
    private TestClock _clock = null!;

    /// <summary>Creates an entered player with identified persisted account and attribute definitions.</summary>
    /// <returns>The setup task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        var game = GameContextTestHelper.CreateGameContext();
        using var setup = game.PersistenceContextProvider.CreateNewContext();
        foreach (var attribute in new[] { Stats.IsVip, Stats.BonusExperienceRate, Stats.MoneyAmountRate })
        {
            game.Configuration.Attributes.Add(setup.CreateNew<AttributeDefinition>(attribute.Id, attribute.Designation, attribute.Description));
        }

        this._player = await PlayerTestHelper.CreatePlayerAsync(game).ConfigureAwait(false);
        var account = this._player.PersistenceContext.CreateNew<Account>();
        account.LoginName = "ann";
        account.WCoinC = 1000;
        this._player.Account = account;
        await this._player.PersistenceContext.SaveChangesAsync().ConfigureAwait(false);
        this._configuration = new() { Enabled = true };
        this._store = VipStateStore.CreateInMemory();
        this._clock = new();
        this._actions = new(this._configuration, this._store, this._clock);
    }

    /// <summary>Releases player timers and persistence.</summary>
    /// <returns>The cleanup task.</returns>
    [TearDown]
    public async Task TearDownAsync() => await this._player.DisposeAsync().ConfigureAwait(false);

    /// <summary>One reviewed game purchase applies actual live XP/zen grants and retry never adds a second debit.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PurchaseRefreshesLiveBenefitsAndReplaysAsync()
    {
        var attributes = this._player.Attributes!;
        var xp = attributes[Stats.BonusExperienceRate];
        var zen = attributes[Stats.MoneyAmountRate];
        var request = await this.SelectionAsync().ConfigureAwait(false);
        using var first = JsonDocument.Parse((await this._actions.HandleAsync(this._player, request).ConfigureAwait(false))!);
        using var replay = JsonDocument.Parse((await this._actions.HandleAsync(this._player, request).ConfigureAwait(false))!);
        Assert.That(first.RootElement.GetProperty("status").GetString(), Is.EqualTo("Applied"));
        Assert.That(replay.RootElement.GetProperty("status").GetString(), Is.EqualTo("Applied"));
        Assert.That(this._player.Account!.WCoinC, Is.EqualTo(900));
        Assert.That(this._player.Account.AttributeGrants, Has.Count.EqualTo(3));
        Assert.That(this._player.Account.AttributeGrants.Select(grant => grant.ValidUntil).Distinct().Count(), Is.EqualTo(1));
        Assert.That(attributes[Stats.IsVip], Is.EqualTo(1));
        Assert.That(attributes[Stats.BonusExperienceRate], Is.EqualTo(xp + 0.05f).Within(0.00001));
        Assert.That(attributes[Stats.MoneyAmountRate], Is.EqualTo(zen + 0.05f).Within(0.00001));
        var history = await this._player.PersistenceContext.GetCashShopCoinHistoryAsync(this._player.Account.GetId(), 0, 20).ConfigureAwait(false);
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].AppliedAt, Is.Not.Null);
    }

    /// <summary>Daily game claims use the same unique account/Sydney-day pending coin grant as the website.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DailyClaimCreditsOnceThroughNormalCoinApplicationAsync()
    {
        await this._actions.HandleAsync(this._player, await this.SelectionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        using var first = JsonDocument.Parse((await this._actions.HandleAsync(this._player, new("daily")).ConfigureAwait(false))!);
        using var retry = JsonDocument.Parse((await this._actions.HandleAsync(this._player, new("daily")).ConfigureAwait(false))!);
        Assert.That(first.RootElement.GetProperty("status").GetString(), Is.EqualTo("DailyApplied"));
        Assert.That(retry.RootElement.GetProperty("status").GetString(), Is.EqualTo("AlreadyClaimed"));
        Assert.That(this._player.Account!.GoblinPoints, Is.EqualTo(this._configuration.Levels[0].DailyCoins));
        Assert.That(await this._player.PersistenceContext.GetPendingCashShopCoinGrantsAsync(this._player.Account.GetId()).ConfigureAwait(false), Is.Empty);
    }

    /// <summary>Disabling new purchases cannot charge a previously reviewed selection.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task DisabledOfferCannotChargeAsync()
    {
        var request = await this.SelectionAsync().ConfigureAwait(false);
        this._configuration.Enabled = false;
        using var result = JsonDocument.Parse((await this._actions.HandleAsync(this._player, request).ConfigureAwait(false))!);
        Assert.That(result.RootElement.GetProperty("status").GetString(), Is.EqualTo("Refused"));
        Assert.That(this._player.Account!.WCoinC, Is.EqualTo(1000));
        Assert.That(this._player.Account.AttributeGrants, Is.Empty);
    }

    /// <summary>Missing offer tokens and unsupported actions cannot mutate an authenticated account.</summary>
    /// <param name="action">The untrusted action.</param>
    /// <returns>The test task.</returns>
    [TestCase("purchase")]
    [TestCase("grant")]
    public async Task InvalidSelectionCannotChargeAsync(string action)
    {
        Assert.That(await this._actions.HandleAsync(this._player, new(action, Guid.NewGuid(), 1, 7)).ConfigureAwait(false), Is.Null);
        Assert.That(this._player.Account!.WCoinC, Is.EqualTo(1000));
    }

    /// <summary>A queued request is refused after the player leaves the world.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task LeftWorldCannotPurchaseAsync()
    {
        var request = await this.SelectionAsync().ConfigureAwait(false);
        this._player.Account = null;
        Assert.That(await this._actions.HandleAsync(this._player, request).ConfigureAwait(false), Is.Null);
    }

    /// <summary>Two purchases reviewed from the same expiry cannot both change that expiry.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task ConcurrentSelectionsUsePersistenceLockAndOfferVersionAsync()
    {
        var request = await this.SelectionAsync().ConfigureAwait(false);
        var responses = await Task.WhenAll(this._actions.HandleAsync(this._player, request).AsTask(), this._actions.HandleAsync(this._player, request with { OperationId = Guid.NewGuid() }).AsTask()).ConfigureAwait(false);
        var statuses = responses.Select(json => JsonDocument.Parse(json!)).ToArray();
        try
        {
            Assert.That(statuses.Select(json => json.RootElement.GetProperty("status").GetString()), Is.EquivalentTo(new[] { "Applied", "PriceChanged" }));
            Assert.That(this._player.Account!.WCoinC, Is.EqualTo(900));
        }
        finally
        {
            foreach (var document in statuses)
            {
                document.Dispose();
            }
        }
    }

    /// <summary>Start and last-day notices are localized, use Sydney time, and do not repeat on every poll.</summary>
    /// <param name="language">The player's language.</param>
    /// <returns>The test task.</returns>
    [TestCase("en")]
    [TestCase("vi")]
    public async Task NoticesUsePaidExpiryAndDoNotRepeatAsync(string language)
    {
        await this._actions.HandleAsync(this._player, await this.SelectionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        this._player.Culture = CultureInfo.GetCultureInfo(language);
        var notices = new VipNotices(this._clock);
        var messages = Mock.Get(this._player.ViewPlugIns.GetPlugIn<IShowMessagePlugIn>()!);
        var received = new List<string>();
        messages.Setup(view => view.ShowMessageAsync(It.IsAny<string>(), MessageType.BlueNormal)).Callback<string, MessageType>((text, _) => received.Add(text)).Returns(ValueTask.CompletedTask);
        await this._player.RunPersistenceExclusiveAsync(() => notices.ShowOwnedAsync(this._player, this._configuration, this._store)).ConfigureAwait(false);
        await this._player.RunPersistenceExclusiveAsync(() => notices.ShowOwnedAsync(this._player, this._configuration, this._store)).ConfigureAwait(false);
        Assert.That(received, Has.Count.EqualTo(1));
        Assert.That(received[0], Does.Contain(language == "vi" ? "có hiệu lực" : "is active"));
        Assert.That(received[0], Does.Contain(language == "vi" ? "Đồng" : "Bronze"));
        var expiry = this._player.Account!.AttributeGrants.First().ValidUntil!.Value;
        var date = TimeZoneInfo.ConvertTimeFromUtc(expiry, HnmoStateStore.ResolveTimeZone(null)).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        Assert.That(received[0], Does.Contain(date));
        this._clock.Advance(TimeSpan.FromDays(6).Add(TimeSpan.FromMinutes(1)));
        await this._player.RunPersistenceExclusiveAsync(() => notices.ShowOwnedAsync(this._player, this._configuration, this._store)).ConfigureAwait(false);
        await this._player.RunPersistenceExclusiveAsync(() => notices.ShowOwnedAsync(this._player, this._configuration, this._store)).ConfigureAwait(false);
        Assert.That(received, Has.Count.EqualTo(2));
        Assert.That(received[1], Does.Contain(language == "vi" ? "hết hạn trong vòng một ngày" : "ends within one day"));
        Assert.That(received[1], Does.Contain(date));
        this._clock.Advance(TimeSpan.FromDays(1));
        await this._player.RunPersistenceExclusiveAsync(() => notices.ShowOwnedAsync(this._player, this._configuration, this._store)).ConfigureAwait(false);
        Assert.That(received, Has.Count.EqualTo(2));
    }

    /// <summary>Chat marks preserve channel markers and disappear when the account grants are removed.</summary>
    /// <param name="type">The original chat channel.</param>
    /// <param name="text">The original message.</param>
    /// <param name="expected">The marked message.</param>
    /// <returns>The test task.</returns>
    [TestCase(ChatMessageType.Normal, "hello", "[VIP] hello")]
    [TestCase(ChatMessageType.Whisper, "private", "[VIP] private")]
    [TestCase(ChatMessageType.Guild, "@guild", "@[VIP] guild")]
    [TestCase(ChatMessageType.Alliance, "@@alliance", "@@[VIP] alliance")]
    [TestCase(ChatMessageType.Gens, "$gens", "$[VIP] gens")]
    [TestCase(ChatMessageType.GlobalNotification, "system", "system")]
    public async Task ChatBadgeUsesLiveEntitlementAsync(ChatMessageType type, string text, string expected)
    {
        Assert.That(VipChat.Mark(this._player, text, type), Is.EqualTo(text));
        await this._actions.HandleAsync(this._player, await this.SelectionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        Assert.That(VipChat.Mark(this._player, text, type), Is.EqualTo(expected));
        this._player.Account!.AttributeGrants.Clear();
        this._player.RefreshAccountAttributeGrants();
        Assert.That(VipChat.Mark(this._player, text, type), Is.EqualTo(text));
    }

    /// <summary>Stopping new sales keeps the paid chat benefit, and the explicit badge switch still hides it.</summary>
    /// <param name="purchasesEnabled">Whether new purchases are allowed.</param>
    /// <returns>The test task.</returns>
    [TestCase(false)]
    [TestCase(true)]
    public async Task ChatViewKeepsPaidBadgeWhenSalesStopAsync(bool purchasesEnabled)
    {
        await this._actions.HandleAsync(this._player, await this.SelectionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        var game = this._player.GameContext;
        game.PlugInManager.RegisterPlugIn<IPlayerStateChangedPlugIn, VipPlugIn>();
        game.PlugInManager.ActivatePlugIn<VipPlugIn>();
        var settings = new VipConfiguration { Enabled = purchasesEnabled, ShowChatBadge = true };
        VipPlugIn.Get(game)!.Configuration = settings;
        var initializer = new MapInitializer(game.Configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var server = new GameServerContext(
            new GameServerDefinition { GameConfiguration = game.Configuration, ServerConfiguration = new GameServerConfiguration() },
            Mock.Of<IGuildServer>(),
            Mock.Of<IEventPublisher>(),
            Mock.Of<ILoginServer>(),
            Mock.Of<IFriendServer>(),
            game.PersistenceContextProvider,
            initializer,
            NullLoggerFactory.Instance,
            game.PlugInManager,
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        initializer.PlugInManager = game.PlugInManager;
        initializer.PathFinderPool = server.PathFinderPool;
        server.PlayersByCharacterName["Hero"] = this._player;
        var first = CastleSiegeRemoteViewTestHelper.CreatePlayer(server);
        await using var recipient = first.Player;
        using var output = first.Output;
        await new ChatViewPlugIn(recipient).ChatMessageAsync("private", "Hero", ChatMessageType.Whisper).ConfigureAwait(false);
        var marked = new ChatMessageRef(output.ToArray());
        Assert.That(marked.Sender, Is.EqualTo("Hero"));
        Assert.That(marked.Message, Is.EqualTo("[VIP] private"));
        settings.ShowChatBadge = false;
        var second = CastleSiegeRemoteViewTestHelper.CreatePlayer(server);
        await using var other = second.Player;
        using var unmarkedOutput = second.Output;
        await new ChatViewPlugIn(other).ChatMessageAsync("private", "Hero", ChatMessageType.Whisper).ConfigureAwait(false);
        Assert.That(new ChatMessageRef(unmarkedOutput.ToArray()).Message, Is.EqualTo("private"));
    }

    /// <summary>Live VIP purchases and grant expiry immediately update the client's Ultimate Autoplay policy.</summary>
    /// <returns>The test task.</returns>
    [Test]
    public async Task PurchaseAndExpiryPushLiveUltimateAutoPlayPolicyAsync()
    {
        var game = this._player.GameContext;
        await game.AddPlayerAsync(this._player).ConfigureAwait(false);
        game.PlugInManager.RegisterPlugIn<IPlayerStateChangedPlugIn, UltimateAutoPlayPlugIn>();
        game.PlugInManager.RegisterPlugIn<IPeriodicTaskPlugIn, UltimateAutoPlayPlugIn>();
        game.PlugInManager.ActivatePlugIn<UltimateAutoPlayPlugIn>();
        var uapPlugIn = UltimateAutoPlayPlugIn.Get(game)!;
        uapPlugIn.Configuration = new UltimateAutoPlayConfiguration();

        var sentPolicies = new List<string>();
        var uapView = this._player.ViewPlugIns.GetPlugIn<IHnmoUltimateAutoPlayViewPlugIn>()!;
        Mock.Get(uapView)
            .Setup(v => v.ShowUltimateAutoPlayPolicyAsync(It.IsAny<string>()))
            .Callback<string>(sentPolicies.Add)
            .Returns(ValueTask.CompletedTask);

        // Initial world entry sends isVip: false.
        await uapPlugIn.PlayerStateChangedAsync(this._player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);
        var countAfterEntry = sentPolicies.Count;
        Assert.That(countAfterEntry, Is.GreaterThanOrEqualTo(1));
        using (var initialDoc = JsonDocument.Parse(sentPolicies[^1]))
        {
            Assert.That(initialDoc.RootElement.GetProperty("isVip").GetBoolean(), Is.False);
        }

        // Buying VIP in-game pushes isVip: true immediately without re-logging or opening Alt+Z.
        var purchase = await this._actions.HandleAsync(this._player, await this.SelectionAsync().ConfigureAwait(false)).ConfigureAwait(false);
        Assert.That(purchase, Is.Not.Null);
        var countAfterPurchase = sentPolicies.Count;
        Assert.That(countAfterPurchase, Is.GreaterThan(countAfterEntry));
        using (var vipDoc = JsonDocument.Parse(sentPolicies[^1]))
        {
            Assert.That(vipDoc.RootElement.GetProperty("isVip").GetBoolean(), Is.True);
        }

        // Removing/expiring VIP and running periodic VIP sync pushes isVip: false, and subsequent no-change ticks don't re-send.
        this._player.Account!.AttributeGrants.Clear();
        this._player.RefreshAccountAttributeGrants();
        uapPlugIn.ForceStart();
        await uapPlugIn.ExecuteTaskAsync((GameContext)game).ConfigureAwait(false);
        var countAfterExpiry = sentPolicies.Count;
        Assert.That(countAfterExpiry, Is.GreaterThan(countAfterPurchase));
        using (var expiredDoc = JsonDocument.Parse(sentPolicies[^1]))
        {
            Assert.That(expiredDoc.RootElement.GetProperty("isVip").GetBoolean(), Is.False);
        }

        uapPlugIn.ForceStart();
        await uapPlugIn.ExecuteTaskAsync((GameContext)game).ConfigureAwait(false);
        Assert.That(sentPolicies, Has.Count.EqualTo(countAfterExpiry));
    }

    private async ValueTask<VipGameRequest> SelectionAsync()
    {
        using var state = JsonDocument.Parse((await this._actions.HandleAsync(this._player, new("state")).ConfigureAwait(false))!);
        var offer = state.RootElement.GetProperty("offers")[0];
        return new("purchase", Guid.NewGuid(), 1, 7, offer.GetProperty("offerToken").GetString());
    }
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public void Advance(TimeSpan interval) => this._now += interval;

        public override DateTimeOffset GetUtcNow() => this._now;
    }

}
