// <copyright file="LoginReservationLeakTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Threading;
using Microsoft.Extensions.Logging;
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
/// Reproduces login-server reservation leaks and concurrent login/disconnect races.
/// </summary>
[TestFixture]
public class LoginReservationLeakTests
{
    private const string LoginName = "test5";
    private const string OtherLoginName = "other5";

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
    /// An exception during session establishment must not advance the failed player to
    /// <see cref="PlayerState.Authenticated"/> or leak the login-server reservation.
    /// </summary>
    [Test]
    public async ValueTask ExceptionDuringSessionEstablishmentDoesNotAdvanceStateOrLeakRegistrationAsync()
    {
        var loginServer = new LoginServer.LoginServer();
        var playerContext = CreateMockPlayerContext(LoginName, throwOnLoad: true);
        var gameContext = this.CreateGameServerContext(playerContext.Object, loginServer);
        var player = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);

        await new LoginAction().LoginAsync(player, LoginName, "pw").ConfigureAwait(false);

        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.LoginScreen));
        Assert.That(player.ReleaseLoginServerRegistration(), Is.Null);
        Assert.That(await loginServer.GetSnapshotAsync().ConfigureAwait(false), Does.Not.ContainKey(LoginName));
    }

    /// <summary>
    /// Sending a login packet while a player is already in the world must not overwrite or leak
    /// its existing login-server registration.
    /// </summary>
    [Test]
    public async ValueTask LoginWhileAlreadyInGameDoesNotOverwriteOrLeakRegistrationAsync()
    {
        var loginServer = new LoginServer.LoginServer();
        var playerContext = CreateMockPlayerContext(LoginName, OtherLoginName);
        var gameContext = this.CreateGameServerContext(playerContext.Object, loginServer);
        var player = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
        WireDisconnectLogOff(player, gameContext);

        await new LoginAction().LoginAsync(player, LoginName, "pw").ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);

        await new LoginAction().LoginAsync(player, OtherLoginName, "pw").ConfigureAwait(false);

        var snapshot = await loginServer.GetSnapshotAsync().ConfigureAwait(false);
        Assert.That(snapshot, Contains.Key(LoginName));
        Assert.That(snapshot, Does.Not.ContainKey(OtherLoginName));

        await player.DisconnectAsync().ConfigureAwait(false);
        Assert.That(await loginServer.GetSnapshotAsync().ConfigureAwait(false), Does.Not.ContainKey(LoginName));
    }

    /// <summary>
    /// Even if a state-change subscriber throws when transitioning to
    /// <see cref="PlayerState.Disconnected"/>, teardown still runs and releases the registration.
    /// </summary>
    [Test]
    public async ValueTask DisconnectWhenStateChangedThrowsStillReleasesRegistrationAsync()
    {
        var loginServer = new LoginServer.LoginServer();
        var playerContext = CreateMockPlayerContext(LoginName);
        var gameContext = this.CreateGameServerContext(playerContext.Object, loginServer);
        var player = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
        WireDisconnectLogOff(player, gameContext);

        await new LoginAction().LoginAsync(player, LoginName, "pw").ConfigureAwait(false);
        player.PlayerState.StateChanged += args =>
        {
            if (args.CurrentStateState == PlayerState.Disconnected)
            {
                throw new InvalidOperationException("Simulated state-changed failure.");
            }

            return ValueTask.CompletedTask;
        };

        try
        {
            await player.DisconnectAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }

        Assert.That(await loginServer.GetSnapshotAsync().ConfigureAwait(false), Does.Not.ContainKey(LoginName));
        Assert.That(await loginServer.TryLoginAsync(LoginName, gameContext.Id).ConfigureAwait(false), Is.True);
    }

    /// <summary>
    /// Disposing a player without <see cref="Player.DisconnectAsync"/> having run its
    /// <see cref="Player.PlayerDisconnected"/> event still releases the login-server registration.
    /// </summary>
    [Test]
    public async ValueTask DisposingPlayerWithoutDisconnectReleasesRegistrationAsync()
    {
        var loginServer = new LoginServer.LoginServer();
        var playerContext = CreateMockPlayerContext(LoginName);
        var gameContext = this.CreateGameServerContext(playerContext.Object, loginServer);
        var player = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);

        await new LoginAction().LoginAsync(player, LoginName, "pw").ConfigureAwait(false);
        Assert.That(await loginServer.GetSnapshotAsync().ConfigureAwait(false), Contains.Key(LoginName));

        await player.DisposeAsync().ConfigureAwait(false);

        Assert.That(await loginServer.GetSnapshotAsync().ConfigureAwait(false), Does.Not.ContainKey(LoginName));
    }

    /// <summary>
    /// Two concurrent logins of the same account where one or both disconnect mid-flight must never
    /// leave the account stuck logged in after both sessions end.
    /// </summary>
    [Test]
    public async ValueTask ConcurrentLoginsAndDisconnectsLeaveAccountLoginableAsync()
    {
        var loginServer = new LoginServer.LoginServer();
        var playerContext = new Mock<IPlayerContext> { DefaultValue = DefaultValue.Mock };
        playerContext
            .Setup(c => c.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, CancellationToken _) =>
            {
                await Task.Yield();
                return AccountState.Normal;
            });
        playerContext
            .Setup(c => c.GetAccountByLoginNameAsync(LoginName, It.IsAny<CancellationToken>()))
            .Returns(async (string name, CancellationToken _) =>
            {
                await Task.Yield();
                return new Persistence.BasicModel.Account { LoginName = name };
            });

        var gameContext = this.CreateGameServerContext(playerContext.Object, loginServer);

        for (var round = 0; round < 20; round++)
        {
            var p1 = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
            var p2 = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
            WireDisconnectLogOff(p1, gameContext);
            WireDisconnectLogOff(p2, gameContext);

            var login1 = new LoginAction().LoginAsync(p1, LoginName, "pw").AsTask();
            var login2 = new LoginAction().LoginAsync(p2, LoginName, "pw").AsTask();

            // Disconnect one or both sessions in the middle of the race.
            if (round % 3 == 0)
            {
                await Task.WhenAll(login1, login2, p1.DisconnectAsync().AsTask(), p2.DisconnectAsync().AsTask()).ConfigureAwait(false);
            }
            else if (round % 3 == 1)
            {
                await Task.WhenAll(login1, login2, p2.DisconnectAsync().AsTask()).ConfigureAwait(false);
                await p1.DisconnectAsync().ConfigureAwait(false);
            }
            else
            {
                await Task.WhenAll(login1, login2).ConfigureAwait(false);
                await p1.DisconnectAsync().ConfigureAwait(false);
                await p2.DisconnectAsync().ConfigureAwait(false);
            }

            Assert.That(
                await loginServer.GetSnapshotAsync().ConfigureAwait(false),
                Does.Not.ContainKey(LoginName),
                $"Round {round}: account stayed registered at login server.");

            var verifier = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
            WireDisconnectLogOff(verifier, gameContext);
            await new LoginAction().LoginAsync(verifier, LoginName, "pw").ConfigureAwait(false);
            Mock.Get(verifier.ViewPlugIns.GetPlugIn<IShowLoginResultPlugIn>()!)
                .Verify(p => p.ShowLoginResultAsync(LoginResult.Ok), Times.Once, $"Round {round}: account could not log in again.");
            await verifier.DisconnectAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// If the login server has a stale registration on this game server with no live player owning it,
    /// a new login recovers it with a Warning log; if a live player owns it or it is on another server,
    /// the login is refused.
    /// </summary>
    [Test]
    public async ValueTask StaleRegistrationWithoutLivePlayerIsRecoveredWithWarningAsync()
    {
        var loginServer = new LoginServer.LoginServer();
        var playerContext = CreateMockPlayerContext(LoginName);
        var logger = new RecordingLogger();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(logger);
        var gameContext = this.CreateGameServerContext(playerContext.Object, loginServer, loggerFactory.Object);

        // Case 1: Live player on this server owns it -> must refuse.
        var liveOwner = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
        WireDisconnectLogOff(liveOwner, gameContext);
        await new LoginAction().LoginAsync(liveOwner, LoginName, "pw").ConfigureAwait(false);

        var contender = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
        await new LoginAction().LoginAsync(contender, LoginName, "pw").ConfigureAwait(false);
        Mock.Get(contender.ViewPlugIns.GetPlugIn<IShowLoginResultPlugIn>()!)
            .Verify(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected), Times.Once);

        // Case 2: Registered on another server -> must refuse.
        await liveOwner.DisconnectAsync().ConfigureAwait(false);
        Assert.That(await loginServer.TryLoginAsync(LoginName, (byte)(gameContext.Id + 1)).ConfigureAwait(false), Is.True);
        var otherServerContender = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
        await new LoginAction().LoginAsync(otherServerContender, LoginName, "pw").ConfigureAwait(false);
        Mock.Get(otherServerContender.ViewPlugIns.GetPlugIn<IShowLoginResultPlugIn>()!)
            .Verify(p => p.ShowLoginResultAsync(LoginResult.AccountAlreadyConnected), Times.Once);

        // Case 3: Stale registration on THIS server with no live player -> recover with Warning.
        await loginServer.LogOffAsync(LoginName, (byte)(gameContext.Id + 1)).ConfigureAwait(false);
        Assert.That(await loginServer.TryLoginAsync(LoginName, gameContext.Id).ConfigureAwait(false), Is.True);

        var recoveringPlayer = await CreatePlayerAtLoginScreenAsync(gameContext).ConfigureAwait(false);
        WireDisconnectLogOff(recoveringPlayer, gameContext);
        await new LoginAction().LoginAsync(recoveringPlayer, LoginName, "pw").ConfigureAwait(false);

        Mock.Get(recoveringPlayer.ViewPlugIns.GetPlugIn<IShowLoginResultPlugIn>()!)
            .Verify(p => p.ShowLoginResultAsync(LoginResult.Ok), Times.Once);
        Assert.That(logger.Warnings, Has.Some.Contains(LoginName));

        await recoveringPlayer.DisconnectAsync().ConfigureAwait(false);
    }

    private static Mock<IPlayerContext> CreateMockPlayerContext(string loginName, string? secondLoginName = null, bool throwOnLoad = false)
    {
        var context = new Mock<IPlayerContext> { DefaultValue = DefaultValue.Mock };
        context
            .Setup(c => c.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccountState.Normal);

        if (throwOnLoad)
        {
            context
                .Setup(c => c.GetAccountByLoginNameAsync(loginName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Simulated DB failure."));
        }
        else
        {
            context
                .Setup(c => c.GetAccountByLoginNameAsync(loginName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Persistence.BasicModel.Account { LoginName = loginName });
            if (secondLoginName is not null)
            {
                context
                    .Setup(c => c.GetAccountByLoginNameAsync(secondLoginName, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Persistence.BasicModel.Account { LoginName = secondLoginName });
            }
        }

        return context;
    }

    private static async ValueTask<Player> CreatePlayerAtLoginScreenAsync(GameServerContext gameContext)
    {
        var player = new LoginTestPlayer(gameContext);
        await gameContext.AddPlayerAsync(player).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        return player;
    }

    private static void WireDisconnectLogOff(Player player, GameServerContext gameContext)
    {
        player.PlayerDisconnected += async disconnected =>
        {
            if (disconnected.ReleaseLoginServerRegistration() is { } name)
            {
                await gameContext.LoginServer.LogOffAsync(name, gameContext.Id).ConfigureAwait(false);
            }

            await disconnected.DisposeAsync().ConfigureAwait(false);
        };
    }

    private GameServerContext CreateGameServerContext(IPlayerContext playerContext, ILoginServer loginServer, ILoggerFactory? loggerFactory = null)
    {
        var gameConfiguration = new Persistence.BasicModel.GameConfiguration();
        gameConfiguration.Maps.Add(new Persistence.BasicModel.GameMapDefinition { TerrainData = new byte[ushort.MaxValue + 3] });
        var effectiveLoggerFactory = loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        var mapInitializer = new MapInitializer(gameConfiguration, effectiveLoggerFactory.CreateLogger<MapInitializer>(), NullDropGenerator.Instance, null);
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
            effectiveLoggerFactory,
            new PlugInManager(new List<PlugInConfiguration>(), effectiveLoggerFactory, null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = context.PlugInManager;
        mapInitializer.PathFinderPool = context.PathFinderPool;
        this._contexts.Add(context);
        return context;
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                lock (this.Warnings)
                {
                    this.Warnings.Add(formatter(state, exception));
                }
            }
        }
    }
}
