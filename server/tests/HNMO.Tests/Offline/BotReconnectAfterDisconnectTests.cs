// <copyright file="BotReconnectAfterDisconnectTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Offline;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Bots;

/// <summary>
/// Verifies that an unexpected disconnect of a bot evicts the stale instance from
/// <see cref="BotManager"/> and queues the bot to be brought back online, while intentional
/// stops (presence rotation or <see cref="BotManager.StopAllAsync"/>) do not.
/// </summary>
[TestFixture]
public class BotReconnectAfterDisconnectTests
{
    /// <summary>
    /// Calling <see cref="Player.DisconnectAsync"/> directly on an active bot clears it from
    /// <see cref="BotManager"/> and brings a fresh session back on respawn.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UnexpectedDisconnect_ClearsBotFromManagerAndRespawnsFreshSessionAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        await CreateBotAccountAsync(gameContext, "bot0001", "BotOne").ConfigureAwait(false);

        var manager = new BotManager();
        try
        {
            var started = await manager.SpawnBotAsync(gameContext, "bot0001", 0).ConfigureAwait(false);
            Assert.That(started, Is.True);

            var first = manager.Bots.Single();
            await first.DisconnectAsync().ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(manager.IsActive("bot0001", 0), Is.False, "A disconnected bot must not remain registered as active.");
                Assert.That(manager.BotCount, Is.EqualTo(0));
            });

            var respawned = await manager.RespawnDisconnectedAsync(gameContext).ConfigureAwait(false);
            var second = manager.Bots.Single();

            Assert.Multiple(() =>
            {
                Assert.That(respawned, Is.EqualTo(1));
                Assert.That(manager.IsActive("bot0001", 0), Is.True);
                Assert.That(manager.BotCount, Is.EqualTo(1));
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(first.IsDisposed, Is.True);
            });
        }
        finally
        {
            await manager.StopAllAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Intentional stops (<see cref="BotManager.StopRandomBotAsync"/> and <see cref="BotManager.StopAllAsync"/>)
    /// must not queue disconnected bots for automatic respawn.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task IntentionalStop_DoesNotQueueBotForRespawnAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        await CreateBotAccountAsync(gameContext, "bot0001", "BotOne").ConfigureAwait(false);

        var manager = new BotManager();
        try
        {
            Assert.That(await manager.SpawnBotAsync(gameContext, "bot0001", 0).ConfigureAwait(false), Is.True);

            var stoppedName = await manager.StopRandomBotAsync().ConfigureAwait(false);
            var respawned = await manager.RespawnDisconnectedAsync(gameContext).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(stoppedName, Is.EqualTo("BotOne"));
                Assert.That(respawned, Is.EqualTo(0));
                Assert.That(manager.BotCount, Is.EqualTo(0));
            });
        }
        finally
        {
            await manager.StopAllAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask CreateBotAccountAsync(IGameContext gameContext, string loginName, string characterName)
    {
        var config = gameContext.Configuration;
        using var ctx = gameContext.PersistenceContextProvider.CreateNewPlayerContext(config);
        var account = ctx.CreateNew<Account>();
        account.LoginName = loginName;
        account.IsBot = true;

        var fallbackClass = ctx.CreateNew<CharacterClass>();
        fallbackClass.HomeMap = config.Maps.First();

        var character = ctx.CreateNew<Character>();
        character.Name = characterName;
        character.CharacterSlot = 0;
        character.CharacterClass = fallbackClass;

        account.Characters.Add(character);
        await ctx.SaveChangesAsync().ConfigureAwait(false);
    }
}
