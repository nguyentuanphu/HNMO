// <copyright file="ChaosCastleMinimumPlayerCountTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.PlayerActions.MiniGames;

/// <summary>
/// Tests that a chaos castle round needs at least two players to start, like the Season 6/9 server suites.
/// With fewer players, the round doesn't start and the entrance fee is given back.
/// </summary>
[TestFixture]
public class ChaosCastleMinimumPlayerCountTests
{
    private const int EntranceFee = 25_000;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly List<MiniGameContext> _gamesToDispose = new();

    /// <summary>
    /// Disposes the games, so that their background loops don't leak into other tests.
    /// </summary>
    [TearDown]
    public async Task TearDownAsync()
    {
        foreach (var game in this._gamesToDispose)
        {
            await game.DisposeAsync().ConfigureAwait(false);
        }

        this._gamesToDispose.Clear();
    }

    /// <summary>
    /// Tests that a round with a single player doesn't start, and that the player gets the entrance fee back.
    /// </summary>
    [Test]
    public async Task SinglePlayerDoesNotStartTheRoundAndGetsTheFeeBackAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var game = this.CreateGame(player.GameContext);
        await EnterAsync(game, player).ConfigureAwait(false);

        var wasPlaying = false;
        var deadline = DateTime.UtcNow.Add(Timeout);
        while (game.State != MiniGameState.Disposed && DateTime.UtcNow < deadline)
        {
            wasPlaying |= game.State == MiniGameState.Playing;
            game.SkipCurrentWait();
            await Task.Delay(20).ConfigureAwait(false);
        }

        Assert.That(wasPlaying, Is.False, "The round started with a single player.");
        Assert.That(game.State, Is.EqualTo(MiniGameState.Disposed));
        Assert.That(player.Money, Is.EqualTo(EntranceFee));
    }

    /// <summary>
    /// Tests that a round with two players starts, and that nobody gets the entrance fee back.
    /// </summary>
    [Test]
    public async Task TwoPlayersStartTheRoundAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var otherPlayer = await PlayerTestHelper.CreatePlayerAsync(player.GameContext).ConfigureAwait(false);
        otherPlayer.SelectedCharacter!.Name = "Other";
        var game = this.CreateGame(player.GameContext);
        await EnterAsync(game, player).ConfigureAwait(false);
        await EnterAsync(game, otherPlayer).ConfigureAwait(false);

        var deadline = DateTime.UtcNow.Add(Timeout);
        while (game.State != MiniGameState.Playing && DateTime.UtcNow < deadline)
        {
            game.SkipCurrentWait();
            await Task.Delay(20).ConfigureAwait(false);
        }

        Assert.That(game.State, Is.EqualTo(MiniGameState.Playing));
        Assert.That(player.Money, Is.EqualTo(0));
        Assert.That(otherPlayer.Money, Is.EqualTo(0));
    }

    private static async Task EnterAsync(MiniGameContext game, Player player)
    {
        player.IsAlive = true;
        Assert.That(await game.TryEnterAsync(player).ConfigureAwait(false), Is.EqualTo(EnterResult.Success));
    }

    private static MiniGameDefinition CreateDefinition(IGameContext gameContext)
    {
        var mapDefinition = gameContext.Configuration.Maps.First(m => m.Number == 0);
        var definitionMock = new Mock<MiniGameDefinition>();
        definitionMock.SetupGet(d => d.Rewards).Returns(new List<MiniGameReward>());
        definitionMock.SetupGet(d => d.SpawnWaves).Returns(new List<MiniGameSpawnWave>());
        definitionMock.SetupGet(d => d.ChangeEvents).Returns(new List<MiniGameChangeEvent>());
        definitionMock.SetupGet(d => d.Entrance).Returns(new ExitGate { Map = mapDefinition });
        var definition = definitionMock.Object;
        definition.Type = MiniGameType.ChaosCastle;
        definition.MapCreationPolicy = MiniGameMapCreationPolicy.Shared;
        definition.EnterDuration = TimeSpan.Zero;
        definition.GameDuration = TimeSpan.FromMinutes(5);
        definition.ExitDuration = TimeSpan.FromMinutes(1);
        definition.MaximumPlayerCount = 70;
        definition.EntranceFee = EntranceFee;

        // MinimumPlayerCount stays 0 (not configured), so the chaos castle default applies.
        return definition;
    }

    private static IMapInitializer CreateMapInitializer()
    {
        var mapInitializerMock = new Mock<IMapInitializer>();
        mapInitializerMock.Setup(m => m.CreateGameMap(It.IsAny<GameMapDefinition>()))
            .Returns<GameMapDefinition>(map => new GameMap(map, TimeSpan.FromMinutes(1), 16));
        return mapInitializerMock.Object;
    }

    private ChaosCastleContext CreateGame(IGameContext gameContext)
    {
        gameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        var game = new ChaosCastleContext(new MiniGameMapKey(0, 0, string.Empty), CreateDefinition(gameContext), gameContext, CreateMapInitializer());
        this._gamesToDispose.Add(game);
        game.EnsureGameLoopRunning();
        return game;
    }
}
