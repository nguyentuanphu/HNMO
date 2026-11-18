// <copyright file="WebLoginTicketTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.Login;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Unit tests for <see cref="WebLoginTicketStore"/> and <see cref="LoginAction.LoginWithTicketAsync"/>.
/// </summary>
[TestFixture]
public class WebLoginTicketTests
{
    private const string LoginName = "ticketuser";

    private readonly List<GameServerContext> _contexts = new();

    /// <summary>
    /// Disposes the game server contexts of the test.
    /// </summary>
    /// <returns>The task.</returns>
    [TearDown]
    public async Task TearDownAsync()
    {
        foreach (var context in this._contexts)
        {
            await context.DisposeAsync().ConfigureAwait(false);
        }

        this._contexts.Clear();
    }

    /// <summary>
    /// Tickets are random (>= 128 bits), single-use, bound to the account, and expire after 60 seconds.
    /// </summary>
    [Test]
    public void TicketStoreEnforcesSingleUseExpiryAndAccountBinding()
    {
        var clock = new FakeClock();
        var store = new WebLoginTicketStore(clock);

        var (ticket1, _) = store.Issue(LoginName);
        var (ticket2, _) = store.Issue(LoginName);
        Assert.That(ticket1, Is.Not.EqualTo(ticket2));
        Assert.That(ticket1.Length, Is.GreaterThanOrEqualTo(22)); // >= 128 bits base64url

        // Wrong ticket fails.
        Assert.That(store.TryRedeem(LoginName, "wrong-ticket"), Is.False);

        // Wrong account fails.
        Assert.That(store.TryRedeem("otheruser", ticket1), Is.False);

        // Single use: second redeem fails.
        Assert.That(store.TryRedeem(LoginName, ticket2), Is.True);
        Assert.That(store.TryRedeem(LoginName, ticket2), Is.False);

        // Expiry: valid at +59s, expired at +60s.
        var (expiring, _) = store.Issue(LoginName);
        clock.Now = clock.Now.AddSeconds(59);
        Assert.That(store.TryRedeem(LoginName, expiring), Is.True);

        var (expired, _) = store.Issue(LoginName);
        clock.Now = clock.Now.AddSeconds(60);
        Assert.That(store.TryRedeem(LoginName, expired), Is.False);
    }

    /// <summary>
    /// A valid ticket logs in without a password, while reused and expired tickets give InvalidPassword
    /// without reserving at the login server.
    /// </summary>
    [Test]
    public async ValueTask LoginWithTicketSucceedsOnceAndRefusesReuseOrExpiredAsync()
    {
        var clock = new FakeClock();
        var store = new WebLoginTicketStore(clock);
        var (ticket, _) = store.Issue(LoginName);

        var loginServer = new LoginServer.LoginServer();
        var playerContext = CreateMockPlayerContext(LoginName, AccountState.Normal);
        var gameContext = this.CreateGameServerContext(playerContext.Object, loginServer);

        var player1 = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
        await new LoginAction().LoginWithTicketAsync(player1, LoginName, ticket, store).ConfigureAwait(false);

        Assert.That(player1.Account?.LoginName, Is.EqualTo(LoginName));
        VerifyLoginResult(player1, LoginResult.Ok);
        await player1.DisconnectAsync().ConfigureAwait(false);
        if (player1.ReleaseLoginServerRegistration() is { } reg)
        {
            await loginServer.LogOffAsync(reg, gameContext.Id).ConfigureAwait(false);
        }

        // Reuse is refused.
        var player2 = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
        await new LoginAction().LoginWithTicketAsync(player2, LoginName, ticket, store).ConfigureAwait(false);
        VerifyLoginResult(player2, LoginResult.InvalidPassword);
        Assert.That(await loginServer.GetSnapshotAsync().ConfigureAwait(false), Does.Not.ContainKey(LoginName));

        // Expired ticket is refused.
        var (expiredTicket, _) = store.Issue(LoginName);
        clock.Now = clock.Now.AddSeconds(61);
        var player3 = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
        await new LoginAction().LoginWithTicketAsync(player3, LoginName, expiredTicket, store).ConfigureAwait(false);
        VerifyLoginResult(player3, LoginResult.InvalidPassword);
        Assert.That(await loginServer.GetSnapshotAsync().ConfigureAwait(false), Does.Not.ContainKey(LoginName));
    }

    /// <summary>
    /// When a ticket login fails after reserving (e.g. account blocked or missing), the login-server
    /// reservation is released.
    /// </summary>
    [TestCase(AccountState.Banned, LoginResult.AccountBlocked)]
    [TestCase(AccountState.TemporarilyBanned, LoginResult.TemporaryBlocked)]
    public async ValueTask TicketLoginFailureReleasesLoginServerReservationAsync(AccountState accountState, LoginResult expectedResult)
    {
        var store = new WebLoginTicketStore();
        var (ticket, _) = store.Issue(LoginName);
        var loginServer = new LoginServer.LoginServer();
        var playerContext = CreateMockPlayerContext(LoginName, accountState);
        var gameContext = this.CreateGameServerContext(playerContext.Object, loginServer);
        var player = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);

        await new LoginAction().LoginWithTicketAsync(player, LoginName, ticket, store).ConfigureAwait(false);

        VerifyLoginResult(player, expectedResult);
        Assert.That(player.ReleaseLoginServerRegistration(), Is.Null);
        Assert.That(await loginServer.GetSnapshotAsync().ConfigureAwait(false), Does.Not.ContainKey(LoginName));
    }

    private static Mock<IPlayerContext> CreateMockPlayerContext(string loginName, AccountState state)
    {
        var context = new Mock<IPlayerContext> { DefaultValue = DefaultValue.Mock };
        context
            .Setup(c => c.GetAccountByLoginNameAsync(loginName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Persistence.BasicModel.Account { LoginName = loginName, State = state });
        return context;
    }

    private static async ValueTask<Player> CreatePlayerAtLoginScreenAsync(GameServerContext gameContext)
    {
        var player = new LoginTestPlayer(gameContext);
        await gameContext.AddPlayerAsync(player).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        return player;
    }

    private static void VerifyLoginResult(Player player, LoginResult result)
    {
        Mock.Get(player.ViewPlugIns.GetPlugIn<IShowLoginResultPlugIn>()!)
            .Verify(p => p.ShowLoginResultAsync(result), Times.Once);
    }

    private GameServerContext CreateGameServerContext(IPlayerContext playerContext, ILoginServer loginServer)
    {
        var gameConfiguration = new Persistence.BasicModel.GameConfiguration();
        gameConfiguration.Maps.Add(new Persistence.BasicModel.GameMapDefinition { TerrainData = new byte[ushort.MaxValue + 3] });
        var mapInitializer = new MapInitializer(gameConfiguration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var context = new GameServerContext(
            new Persistence.BasicModel.GameServerDefinition
            {
                GameConfiguration = gameConfiguration,
                ServerConfiguration = new Persistence.BasicModel.GameServerConfiguration { MaximumPlayers = 10 },
            },
            new Mock<IGuildServer>().Object,
            new Mock<IEventPublisher>().Object,
            loginServer,
            new Mock<IFriendServer>().Object,
            LoginTestSetup.CreatePersistence(playerContext),
            mapInitializer,
            NullLoggerFactory.Instance,
            new PlugInManager(new List<PlugInConfiguration>(), NullLoggerFactory.Instance, null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = context.PlugInManager;
        mapInitializer.PathFinderPool = context.PathFinderPool;
        this._contexts.Add(context);
        return context;
    }

    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => this.Now;
    }
}
