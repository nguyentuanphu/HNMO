// <copyright file="LetterMailboxFullTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using HNMO.DataModel.Configuration;
using System.Threading;
using Moq;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Messenger;
using HNMO.GameLogic.Views.Messenger;
using HNMO.Persistence;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// A letter to a full mailbox (the game configuration's maximum letters, classic 50) is refused with "mailbox full", and
/// the sender keeps the postage; below the maximum it goes on to be saved.
/// </summary>
[TestFixture]
public class LetterMailboxFullTest
{
    /// <summary>A full mailbox refuses the letter.</summary>
    /// <param name="lettersInMailbox">The letters the receiver has.</param>
    /// <param name="refused">Whether the letter is refused as "mailbox full".</param>
    /// <returns>The task.</returns>
    [TestCase(50, true)]
    [TestCase(60, true)]
    [TestCase(49, false)]
    public async Task AFullMailboxRefusesTheLetterAsync(int lettersInMailbox, bool refused)
    {
        var playerContext = new Mock<IPlayerContext> { DefaultValue = DefaultValue.Mock };
        playerContext.Setup(c => c.GetLetterCountAsync("Receiver", It.IsAny<CancellationToken>())).ReturnsAsync(lettersInMailbox);
        playerContext.Setup(c => c.CanSaveLetterAsync(It.IsAny<HNMO.Interfaces.LetterHeader>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var provider = new Mock<IPersistenceContextProvider> { DefaultValue = DefaultValue.Mock };
        provider.Setup(p => p.CreateNewPlayerContext(It.IsAny<GameConfiguration>())).Returns(playerContext.Object);

        var gameConfiguration = PlayerTestHelper.CreateGameConfiguration();
        gameConfiguration.MaximumLetters = 50;
        gameConfiguration.LetterSendPrice = 1000;
        var mapInitializer = new MapInitializer(gameConfiguration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var gameContext = new GameContext(gameConfiguration, provider.Object, mapInitializer, new NullLoggerFactory(), new PlugInManager(null, new NullLoggerFactory(), null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = gameContext.PlugInManager;
        mapInitializer.PathFinderPool = gameContext.PathFinderPool;
        var player = await PlayerTestHelper.CreatePlayerAsync(gameContext).ConfigureAwait(false);
        player.Money = 5000;

        await new LetterSendAction().SendLetterAsync(player, "Receiver", "Hello", "Hi", 0, 0, 7).ConfigureAwait(false);

        var view = Mock.Get(player.ViewPlugIns.GetPlugIn<ILetterSendResultPlugIn>()!);
        view.Verify(v => v.LetterSendResultAsync(LetterSendSuccess.MailboxFull, 7), refused ? Times.Once() : Times.Never());
        playerContext.Verify(c => c.CanSaveLetterAsync(It.IsAny<HNMO.Interfaces.LetterHeader>(), It.IsAny<CancellationToken>()), refused ? Times.Never() : Times.Once());
        Assert.That(player.Money, Is.EqualTo(5000), "no postage for a letter that isn't sent");
    }
}
