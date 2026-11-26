// <copyright file="CoinSyncLocalSaveFailureTests.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Threading;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Seasons;
using HNMO.Persistence;

/// <summary>
/// A Season 9 program adds its coin changes to the shared store while it saves a player's progress. When the local
/// save fails, the shared store must not keep the change: the fee would be paid there while the benefit that was to be
/// saved with it is lost.
/// </summary>
[TestFixture]
public class CoinSyncLocalSaveFailureTests
{
    /// <summary>A fee is not left in the shared store when the local save of the same progress throws.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FeeIsNotKeptInSharedStoreWhenLocalSaveThrowsAsync()
    {
        SeasonSharedAccounts.Reset();
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var account = player.Account!;
        account.LoginName = "feeslogin";
        account.WCoinC = 100;
        var store = new FakeStore(new SharedAccount("feeslogin", string.Empty, string.Empty, string.Empty, AccountState.Normal, null, null, new CoinBalances(100, 0, 0)));
        SharedAccountStore.Current = store;
        try
        {
            await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false); // the baseline of a logged-in account
            account.WCoinC = 60; // a fee of 40; its benefit would be saved in the same save
            CashShopActionsTest.SaveFailingContext.Install(player, () => true);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await player.SaveProgressAsync().ConfigureAwait(false));

            Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(100), "the fee must not stay in the shared store when nothing was saved locally");
        }
        finally
        {
            SharedAccountStore.Current = null;
            SeasonSharedAccounts.Reset();
        }
    }

    /// <summary>After a failed save the next successful save charges the fee once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FeeIsChargedOnceWhenTheSaveIsRetriedAsync()
    {
        SeasonSharedAccounts.Reset();
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var account = player.Account!;
        account.LoginName = "retrylogin";
        account.WCoinC = 100;
        var store = new FakeStore(new SharedAccount("retrylogin", string.Empty, string.Empty, string.Empty, AccountState.Normal, null, null, new CoinBalances(100, 0, 0)));
        SharedAccountStore.Current = store;
        try
        {
            await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false);
            account.WCoinC = 60;
            var fails = true;
            CashShopActionsTest.SaveFailingContext.Install(player, () => fails);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await player.SaveProgressAsync().ConfigureAwait(false));

            fails = false;
            await player.SaveProgressAsync().ConfigureAwait(false);

            Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(60), "40 charged once, not twice and not zero times");
            Assert.That(account.WCoinC, Is.EqualTo(60));
        }
        finally
        {
            SharedAccountStore.Current = null;
            SeasonSharedAccounts.Reset();
        }
    }

    private sealed class FakeStore(SharedAccount initial) : ISharedAccountStore
    {
        public SharedAccount Shared { get; private set; } = initial;

        public ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<SharedAccount?>(this.Shared);

        public ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default)
        {
            var coins = this.Shared.Coins;
            var updated = new CoinBalances(Math.Max(0, coins.WCoinC + change.WCoinC), Math.Max(0, coins.WCoinP + change.WCoinP), Math.Max(0, coins.GoblinPoints + change.GoblinPoints));
            this.Shared = this.Shared with { Coins = updated };
            return ValueTask.FromResult<CoinBalances?>(updated);
        }
    }
}
