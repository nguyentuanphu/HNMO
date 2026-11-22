// <copyright file="BotMiniGameHandlerTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Bots;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlayerActions.MiniGames;
using Moq;

/// <summary>
/// Verifies that bots never enter mini game events (Blood Castle, Devil Square, Chaos Castle)
/// (owner decision, 21 Nov 2026): neither via <see cref="BotMiniGameHandler"/> party following nor
/// via direct <see cref="MiniGameContext.TryEnterAsync"/>.
/// </summary>
[TestFixture]
public class BotMiniGameHandlerTest
{
    /// <summary>
    /// Bots are never eligible to enter Blood Castle, Devil Square, or Chaos Castle regardless of level or state.
    /// </summary>
    /// <param name="type">The event type.</param>
    [TestCase(MiniGameType.BloodCastle)]
    [TestCase(MiniGameType.DevilSquare)]
    [TestCase(MiniGameType.ChaosCastle)]
    public async ValueTask IsEligible_AlwaysRefusesBotsForEventsAsync(MiniGameType type)
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var bot = await CreateBotAsync(gameContext).ConfigureAwait(false);
        bot.Attributes![Stats.Level] = 300;
        var definition = new MiniGameDefinition
        {
            Type = type,
            MinimumCharacterLevel = 1,
            MaximumCharacterLevel = 400,
            ArePlayerKillersAllowedToEnter = true,
        };

        var eligible = BotMiniGameHandler.IsEligible(bot, definition, out var reason);

        Assert.Multiple(() =>
        {
            Assert.That(eligible, Is.False);
            Assert.That(reason, Is.Not.Empty);
        });
    }

    /// <summary>
    /// Even when a human party master carries bots in their party, <see cref="BotMiniGameHandler.SnapshotPartyBots"/>
    /// returns an empty list so bots never follow into events.
    /// </summary>
    [Test]
    public async ValueTask SnapshotReturnsEmptyWhenMasterEntersEventAsync()
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var leader = await CreateHumanAsync(gameContext, "Leader").ConfigureAwait(false);
        var member = await CreateHumanAsync(gameContext, "Member").ConfigureAwait(false);
        var bot1 = await CreateBotAsync(gameContext, "BotOne").ConfigureAwait(false);
        var bot2 = await CreateBotAsync(gameContext, "BotTwo").ConfigureAwait(false);

        var party = gameContext.PartyManager.CreateParty();
        await party.AddAsync(leader).ConfigureAwait(false);
        await party.AddAsync(member).ConfigureAwait(false);
        await party.AddAsync(bot1).ConfigureAwait(false);
        await party.AddAsync(bot2).ConfigureAwait(false);

        Assert.That(BotMiniGameHandler.SnapshotPartyBots(leader), Is.Empty);
        Assert.That(BotMiniGameHandler.SnapshotPartyBots(member), Is.Empty);
    }

    /// <summary>
    /// <see cref="MiniGameContext.TryEnterAsync"/> rejects bot accounts while allowing human players.
    /// </summary>
    [TestCase(MiniGameType.BloodCastle)]
    [TestCase(MiniGameType.DevilSquare)]
    [TestCase(MiniGameType.ChaosCastle)]
    public async ValueTask MiniGameContext_RefusesBotEntryAsync(MiniGameType type)
    {
        var gameContext = GameContextTestHelper.CreateGameContext();
        var bot = await CreateBotAsync(gameContext, "BotOne").ConfigureAwait(false);
        var human = await CreateHumanAsync(gameContext, "Human").ConfigureAwait(false);
        var definition = CreateMiniGameDefinition(gameContext, type);

        var miniGame = await gameContext.MiniGames.GetOrCreateAsync(definition, human).ConfigureAwait(false);
        try
        {
            var botResult = await miniGame.TryEnterAsync(bot).ConfigureAwait(false);
            var humanResult = await miniGame.TryEnterAsync(human).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(botResult, Is.EqualTo(EnterResult.Failed));
                Assert.That(bot.CurrentMiniGame, Is.Null);
                Assert.That(humanResult, Is.EqualTo(EnterResult.Success));
                Assert.That(human.CurrentMiniGame, Is.SameAs(miniGame));
            });
        }
        finally
        {
            await miniGame.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static MiniGameDefinition CreateMiniGameDefinition(IGameContext gameContext, MiniGameType type)
    {
        var definitionMock = new Mock<MiniGameDefinition>();
        definitionMock.SetupGet(d => d.Rewards).Returns(new List<MiniGameReward>());
        definitionMock.SetupGet(d => d.SpawnWaves).Returns(new List<MiniGameSpawnWave>());
        definitionMock.SetupGet(d => d.ChangeEvents).Returns(new List<MiniGameChangeEvent>());
        definitionMock.SetupGet(d => d.Entrance).Returns(new ExitGate { Map = gameContext.Configuration.Maps.Single() });
        var definition = definitionMock.Object;
        definition.Type = type;
        definition.MapCreationPolicy = MiniGameMapCreationPolicy.Shared;
        definition.EnterDuration = TimeSpan.FromMinutes(1);
        definition.GameDuration = TimeSpan.FromMinutes(5);
        definition.ExitDuration = TimeSpan.FromMinutes(1);
        definition.MaximumPlayerCount = 10;
        return definition;
    }

    private static async ValueTask<OfflinePlayer> CreateBotAsync(IGameContext gameContext, string name = "Bot")
    {
        var bot = await PlayerTestHelper.CreateOfflineLevelingPlayerAsync(gameContext).ConfigureAwait(false);
        await bot.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        bot.SelectedCharacter!.Name = name;
        bot.IsAlive = true;
        bot.Account!.IsBot = true;
        return bot;
    }

    private static async ValueTask<Player> CreateHumanAsync(IGameContext gameContext, string name)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        player.SelectedCharacter!.Name = name;
        player.IsAlive = true;
        return player;
    }
}
