// <copyright file="EconomyIntegrationCoinTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Threading;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Seasons;
using HNMO.Interfaces;
using HNMO.Persistence;
using Moq;

/// <summary>
/// Coin flows between a Season 9 program and the shared (Season 6.3) store, with #373, #376 and #377 applied together.
/// Every assertion is on the balances in the shared store and in the account after the operation.
/// </summary>
[TestFixture]
public class EconomyIntegrationCoinTests
{
    /// <summary>A failed shared write leaves the change for the next sync, which applies it exactly once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailedSharedWriteIsRetriedOnceAsync()
    {
        var (account, store) = await StartAsync("retry", 100).ConfigureAwait(false);
        account.WCoinC = 60; // a fee of 40

        store.FailNext = true;
        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false), Is.False);
        Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(100), "nothing reached the shared store");
        Assert.That(account.WCoinC, Is.EqualTo(60), "the account keeps the fee");

        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false), Is.True);
        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false), Is.True);
        Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(60), "the fee is applied once, not twice");
        Assert.That(account.WCoinC, Is.EqualTo(60));
    }

    /// <summary>A deposit made from the other season while a fee is pending survives the sync.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DepositFromOtherSeasonSurvivesPendingFeeAsync()
    {
        var (account, store) = await StartAsync("deposit", 100).ConfigureAwait(false);
        account.WCoinC = 70; // a fee of 30 in this season
        store.Shared = store.Shared with { Coins = new CoinBalances(150, 0, 0) }; // +50 bought in the other season

        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false), Is.True);

        Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(120), "150 - 30");
        Assert.That(account.WCoinC, Is.EqualTo(120), "the account shows the shared balance");
    }

    /// <summary>
    /// Characterization of the order in <c>PlayerPersistence.SaveProgressAsync</c> (shared store first, then the local save):
    /// when the local save is lost (restart) after the shared write, the next login takes the shared balance, so a fee
    /// stays paid although the local save of its benefit is gone. The test pins this behaviour; see the report.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SharedBalanceWinsAfterLocalSaveWasLostAsync()
    {
        var (account, store) = await StartAsync("lost", 100).ConfigureAwait(false);
        account.WCoinC = 60; // fee of 40, benefit not yet saved locally
        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false), Is.True);

        // The program restarts before the local save: the local row still has the old balance.
        SeasonSharedAccounts.Reset();
        var local = new Account { LoginName = "lost", WCoinC = 100 };
        var ctx = new Mock<IPlayerContext>();
        ctx.Setup(c => c.GetAccountByLoginNameAsync("lost", It.IsAny<CancellationToken>())).ReturnsAsync(local);
        ctx.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await SeasonSharedAccounts.MirrorAsync(store, () => ctx.Object, "lost").ConfigureAwait(false);

        Assert.That(local.WCoinC, Is.EqualTo(60), "the 40 stay spent");
    }

    private static async Task<(Account Account, FakeStore Store)> StartAsync(string login, int coins)
    {
        SeasonSharedAccounts.Reset();
        var account = new Account { LoginName = login, WCoinC = coins };
        var ctx = new Mock<IPlayerContext>();
        ctx.Setup(c => c.GetAccountByLoginNameAsync(login, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        ctx.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var store = new FakeStore(new SharedAccount(login, string.Empty, string.Empty, string.Empty, AccountState.Normal, null, null, new CoinBalances(coins, 0, 0)));
        await SeasonSharedAccounts.MirrorAsync(store, () => ctx.Object, login).ConfigureAwait(false);
        return (account, store);
    }

    private sealed class FakeStore(SharedAccount initial) : ISharedAccountStore
    {
        public SharedAccount Shared { get; set; } = initial;

        public bool FailNext { get; set; }

        public ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<SharedAccount?>(this.Shared);

        public ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default)
        {
            if (this.FailNext)
            {
                this.FailNext = false;
                return ValueTask.FromResult<CoinBalances?>(null);
            }

            var coins = this.Shared.Coins;
            var updated = new CoinBalances(Math.Max(0, coins.WCoinC + change.WCoinC), Math.Max(0, coins.WCoinP + change.WCoinP), Math.Max(0, coins.GoblinPoints + change.GoblinPoints));
            this.Shared = this.Shared with { Coins = updated };
            return ValueTask.FromResult<CoinBalances?>(updated);
        }
    }
}
