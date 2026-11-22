// <copyright file="BotProofOfConceptSessionTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Offline;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Bots;
using HNMO.GameLogic.Offline;
using HNMO.Persistence.InMemory;

/// <summary>
/// Verifies that animating an existing non-bot account via <c>ProofOfConceptAccounts</c>
/// never creates a double session when a client or offline player is already active,
/// and blocks concurrent client logins until the bot stops.
/// </summary>
[TestFixture]
public class BotProofOfConceptSessionTests
{
    /// <summary>
    /// Does not spawn a bot for an existing non-bot account when it is already logged in on the login server.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SpawnBotAsync_SkipsNonBotAccountAlreadyLoggedInOnLoginServerAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var loginServer = new LoginServer.LoginServer();
        using var gameContext = LoginTestSetup.CreateGameServerContext(persistence, loginServer);
        await PersistAccountAsync(gameContext, "alice", isBot: false).ConfigureAwait(false);
        await loginServer.TryLoginAsync("alice", gameContext.Id).ConfigureAwait(false);

        var manager = new BotManager();
        try
        {
            var spawned = await manager.SpawnBotAsync(gameContext, "alice").ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(spawned, Is.False, "An already logged-in account must not get a second bot session.");
                Assert.That(manager.BotCount, Is.EqualTo(0));
            });
        }
        finally
        {
            await manager.StopAllAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Claims the login-server registration when spawning a proof-of-concept account so a real client
    /// or second slot cannot create a double session, and releases the registration when stopped.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SpawnBotAsync_RegistersNonBotAccountOnLoginServerAndReleasesOnStopAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var loginServer = new LoginServer.LoginServer();
        using var gameContext = LoginTestSetup.CreateGameServerContext(persistence, loginServer);
        await PersistAccountAsync(gameContext, "alice", isBot: false, includeSecondSlot: true).ConfigureAwait(false);

        var manager = new BotManager();
        try
        {
            var spawned = await manager.SpawnBotAsync(gameContext, "alice", allowNonBotAccount: true).ConfigureAwait(false);
            var secondSlotSpawned = await manager.SpawnBotAsync(gameContext, "alice", 1, allowNonBotAccount: true).ConfigureAwait(false);
            var concurrentLoginAllowed = await loginServer.TryLoginAsync("alice", 2).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(spawned, Is.True);
                Assert.That(secondSlotSpawned, Is.False, "A second character slot on the same non-bot account must not spawn.");
                Assert.That(concurrentLoginAllowed, Is.False, "LoginServer must reject a concurrent login while the bot animates the account.");
            });
        }
        finally
        {
            await manager.StopAllAsync().ConfigureAwait(false);
        }

        var loginAfterStopAllowed = await loginServer.TryLoginAsync("alice", 2).ConfigureAwait(false);
        Assert.That(loginAfterStopAllowed, Is.True, "Stopping the bot must release the login-server registration.");
    }

    /// <summary>
    /// Does not spawn a bot for an account that currently has an active offline (<c>/offlevel</c>/<c>/offtrade</c>) session.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SpawnBotAsync_SkipsAccountWithActiveOfflinePlayerSessionAsync()
    {
        var persistence = new InMemoryPersistenceContextProvider();
        var loginServer = new LoginServer.LoginServer();
        using var gameContext = LoginTestSetup.CreateGameServerContext(persistence, loginServer);
        await PersistAccountAsync(gameContext, "alice", isBot: false).ConfigureAwait(false);

        var existingOffline = new OfflinePlayer(gameContext);
        gameContext.OfflinePlayerManager.TryAdd("alice", existingOffline);

        var manager = new BotManager();
        try
        {
            var spawned = await manager.SpawnBotAsync(gameContext, "alice").ConfigureAwait(false);
            Assert.That(spawned, Is.False, "An active /offlevel or /offtrade session must block spawning a PoC bot.");
        }
        finally
        {
            await manager.StopAllAsync().ConfigureAwait(false);
            await existingOffline.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask PersistAccountAsync(IGameContext gameContext, string loginName, bool isBot, bool includeSecondSlot = false)
    {
        var config = gameContext.Configuration;
        using var ctx = gameContext.PersistenceContextProvider.CreateNewPlayerContext(config);
        var account = ctx.CreateNew<Account>();
        account.LoginName = loginName;
        account.IsBot = isBot;

        var fallbackClass = ctx.CreateNew<CharacterClass>();
        fallbackClass.HomeMap = config.Maps.First();

        var first = ctx.CreateNew<Character>();
        first.Name = $"{loginName}_0";
        first.CharacterSlot = 0;
        first.CharacterClass = fallbackClass;
        account.Characters.Add(first);

        if (includeSecondSlot)
        {
            var second = ctx.CreateNew<Character>();
            second.Name = $"{loginName}_1";
            second.CharacterSlot = 1;
            second.CharacterClass = fallbackClass;
            account.Characters.Add(second);
        }

        await ctx.SaveChangesAsync().ConfigureAwait(false);
    }
}
