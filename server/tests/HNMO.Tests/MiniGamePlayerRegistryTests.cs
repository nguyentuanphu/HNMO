// <copyright file="MiniGamePlayerRegistryTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.PlayerActions.MiniGames;
using HNMO.Persistence.InMemory;

/// <summary>
/// Tests for the <see cref="MiniGamePlayerRegistry"/> entering policy: entering is only
/// possible while the game is open and not full, atomically with state transitions.
/// </summary>
[TestFixture]
public class MiniGamePlayerRegistryTests
{
    /// <summary>
    /// Tests that entering works while the game is open.
    /// </summary>
    [Test]
    public async Task EnterWhileOpenSucceedsAsync()
    {
        var registry = new MiniGamePlayerRegistry(this.CreateDefinition());

        var result = await registry.TryEnterAsync(CreatePlayer(), _ => ValueTask.FromResult(true)).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(EnterResult.Success));
    }

    /// <summary>
    /// Tests that entering fails once the entrance has been closed.
    /// </summary>
    [Test]
    public async Task EnterAfterCloseIsRejectedAsync()
    {
        var registry = new MiniGamePlayerRegistry(this.CreateDefinition());
        await registry.SetStateAsync(MiniGameState.Closed).ConfigureAwait(false);

        var result = await registry.TryEnterAsync(CreatePlayer(), _ => ValueTask.FromResult(true)).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(EnterResult.NotOpen));
    }

    /// <summary>
    /// Tests that entering works while closed when entering while playing is allowed,
    /// for the instant start which passes through the closed state in milliseconds.
    /// </summary>
    [Test]
    public async Task EnterWhileClosedSucceedsWhenAllowedAsync()
    {
        var registry = new MiniGamePlayerRegistry(this.CreateDefinition());
        await registry.SetStateAsync(MiniGameState.Closed).ConfigureAwait(false);

        var result = await registry.TryEnterAsync(CreatePlayer(), _ => ValueTask.FromResult(true), () => true).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(EnterResult.Success));
    }

    /// <summary>
    /// Tests that entering fails when the game is full with other players.
    /// </summary>
    [Test]
    public async Task EnterWhenFullIsRejectedAsync()
    {
        var registry = new MiniGamePlayerRegistry(this.CreateDefinition());
        Assert.That(await registry.TryEnterAsync(CreatePlayer(), _ => ValueTask.FromResult(true)).ConfigureAwait(false), Is.EqualTo(EnterResult.Success));

        var result = await registry.TryEnterAsync(CreatePlayer(), _ => ValueTask.FromResult(true)).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(EnterResult.Full));
    }

    /// <summary>
    /// Tests that entering while the game runs is rejected by default.
    /// </summary>
    [Test]
    public async Task EnterWhilePlayingIsRejectedByDefaultAsync()
    {
        var registry = new MiniGamePlayerRegistry(this.CreateDefinition());
        await registry.SetStateAsync(MiniGameState.Playing).ConfigureAwait(false);

        var result = await registry.TryEnterAsync(CreatePlayer(), _ => ValueTask.FromResult(true)).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(EnterResult.NotOpen));
    }

    /// <summary>
    /// Tests that entering while the game runs succeeds when late entering is allowed,
    /// e.g. to rejoin an ongoing event.
    /// </summary>
    [Test]
    public async Task EnterWhilePlayingWithFlagSucceedsAsync()
    {
        var registry = new MiniGamePlayerRegistry(this.CreateDefinition());
        await registry.SetStateAsync(MiniGameState.Playing).ConfigureAwait(false);

        var result = await registry.TryEnterAsync(CreatePlayer(), _ => ValueTask.FromResult(true), allowEnterWhilePlaying: () => true).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(EnterResult.Success));
    }

    /// <summary>
    /// Tests that the late entering flag doesn't open ended games.
    /// </summary>
    [Test]
    public async Task EnterWhenEndedWithFlagIsRejectedAsync()
    {
        var registry = new MiniGamePlayerRegistry(this.CreateDefinition());
        await registry.SetStateAsync(MiniGameState.Ended).ConfigureAwait(false);

        var result = await registry.TryEnterAsync(CreatePlayer(), _ => ValueTask.FromResult(true), allowEnterWhilePlaying: () => true).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(EnterResult.NotOpen));
    }

    private static Player CreatePlayer()
    {
        var contextMock = new Mock<IGameContext>();
        contextMock.SetupGet(c => c.LoggerFactory).Returns(NullLoggerFactory.Instance);
        contextMock.SetupGet(c => c.Configuration).Returns(new GameConfiguration());
        contextMock.SetupGet(c => c.PersistenceContextProvider).Returns(new InMemoryPersistenceContextProvider());
        return new Player(contextMock.Object);
    }

    private MiniGameDefinition CreateDefinition()
    {
        return new MiniGameDefinition { MaximumPlayerCount = 1 };
    }
}
