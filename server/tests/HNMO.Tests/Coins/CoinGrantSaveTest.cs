// <copyright file="CoinGrantSaveTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Coins;

using System.Threading;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Coins are never reported as given when their grant wasn't saved (audit 1.10): the gift code's waiting coins are
/// kept and tried again.
/// </summary>
[TestFixture]
public class CoinGrantSaveTest
{
    /// <summary>A save that reports failure gives <see cref="GiftCodeCoinsResult.Failed"/>; a good one, Given.</summary>
    /// <param name="saved">What the save reports.</param>
    /// <param name="expected">The result.</param>
    [TestCase(false, GiftCodeCoinsResult.Failed)]
    [TestCase(true, GiftCodeCoinsResult.Given)]
    public async Task GiftCodeCoinsCountOnlyWhenSavedAsync(bool saved, GiftCodeCoinsResult expected)
    {
        var context = new Mock<IPlayerContext>();
        context.Setup(c => c.GetCashShopCoinGrantByReferenceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((CashShopCoinGrant?)null);
        context.Setup(c => c.GetAccountIdByLoginNameAsync("player", It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        context.Setup(c => c.CreateNew<CashShopCoinGrant>(It.IsAny<object?[]>())).Returns(new CashShopCoinGrant());
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        var persistence = new Mock<IPersistenceContextProvider>();
        persistence.Setup(p => p.CreateNewPlayerContext(It.IsAny<GameConfiguration>())).Returns(context.Object);

        var coins = new GiftCodePendingCoins { Reference = "gift:1", Login = "player", CoinType = CashShopCoinType.WCoinC, Amount = 100, Reason = "test" };
        var result = await GiftCodeRewards.GrantCoinsAsync(persistence.Object, new GameConfiguration(), coins, NullLogger.Instance).ConfigureAwait(false);

        Assert.That(result, Is.EqualTo(expected));
    }
}
