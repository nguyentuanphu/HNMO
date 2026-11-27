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

    /// <summary>
    /// The shared store keeps no balance below zero (PostgreSQL's GREATEST): when the other season spent coins meanwhile,
    /// a fee is only partly taken there. Taking it back must give back what was taken, not the whole fee, or coins come
    /// out of nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoGivesBackOnlyWhatTheSharedStoreTookAsync()
    {
        SeasonSharedAccounts.Reset();
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var account = player.Account!;
        account.LoginName = "clamplogin";
        account.WCoinC = 100;
        var store = new FakeStore(new SharedAccount("clamplogin", string.Empty, string.Empty, string.Empty, AccountState.Normal, null, null, new CoinBalances(100, 0, 0)));
        SharedAccountStore.Current = store;
        try
        {
            await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false);
            store.Set(new CoinBalances(30, 0, 0)); // 70 spent in the other season
            account.WCoinC = 60; // a fee of 40 here
            CashShopActionsTest.SaveFailingContext.Install(player, () => true);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await player.SaveProgressAsync().ConfigureAwait(false));

            Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(30), "the 30 the shared store took come back, not the 40 of the fee");
        }
        finally
        {
            SharedAccountStore.Current = null;
            SeasonSharedAccounts.Reset();
        }
    }

    /// <summary>
    /// When the shared store refuses to take the fee back after the local save failed, the fee stays there once and the
    /// running game keeps it too: the next successful save charges nothing more, and both sides end up the same.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFailedUndoChargesTheFeeOnlyOnceAsync()
    {
        SeasonSharedAccounts.Reset();
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var account = player.Account!;
        account.LoginName = "undofails";
        account.WCoinC = 100;
        var store = new FakeStore(new SharedAccount("undofails", string.Empty, string.Empty, string.Empty, AccountState.Normal, null, null, new CoinBalances(100, 0, 0)));
        SharedAccountStore.Current = store;
        try
        {
            await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false);
            account.WCoinC = 60;
            var fails = true;
            CashShopActionsTest.SaveFailingContext.Install(player, () => fails);
            store.RefuseAddsAfter(1); // the fee goes in, the undo is refused
            Assert.ThrowsAsync<InvalidOperationException>(async () => await player.SaveProgressAsync().ConfigureAwait(false));
            Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(60), "the refused undo leaves the fee in the shared store");

            store.RefuseAddsAfter(int.MaxValue);
            fails = false;
            await player.SaveProgressAsync().ConfigureAwait(false);

            Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(60), "the next save charges nothing more");
            Assert.That(account.WCoinC, Is.EqualTo(60));
        }
        finally
        {
            SharedAccountStore.Current = null;
            SeasonSharedAccounts.Reset();
        }
    }

    /// <summary>The three coin kinds go through the same sync: each fee is charged once after a failed save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AllCoinKindsAreChargedOnceAfterAFailedSaveAsync()
    {
        SeasonSharedAccounts.Reset();
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var account = player.Account!;
        account.LoginName = "allkinds";
        account.WCoinC = 100;
        account.WCoinP = 50;
        account.GoblinPoints = 20;
        var store = new FakeStore(new SharedAccount("allkinds", string.Empty, string.Empty, string.Empty, AccountState.Normal, null, null, new CoinBalances(100, 50, 20)));
        SharedAccountStore.Current = store;
        try
        {
            await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false);
            account.WCoinC = 90;
            account.WCoinP = 45;
            account.GoblinPoints = 18;
            var fails = true;
            CashShopActionsTest.SaveFailingContext.Install(player, () => fails);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await player.SaveProgressAsync().ConfigureAwait(false));
            Assert.That(store.Shared.Coins, Is.EqualTo(new CoinBalances(100, 50, 20)), "nothing stays charged after the failed save");

            fails = false;
            await player.SaveProgressAsync().ConfigureAwait(false);

            Assert.That(store.Shared.Coins, Is.EqualTo(new CoinBalances(90, 45, 18)));
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

        public void Set(CoinBalances coins) => this.Shared = this.Shared with { Coins = coins };

        public ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<SharedAccount?>(this.Shared);

        private int _addsLeft = int.MaxValue;

        public void RefuseAddsAfter(int count) => this._addsLeft = count;

        public ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default)
        {
            if (this._addsLeft <= 0)
            {
                return ValueTask.FromResult<CoinBalances?>(null);
            }

            this._addsLeft--;
            var coins = this.Shared.Coins;
            var updated = new CoinBalances(Math.Max(0, coins.WCoinC + change.WCoinC), Math.Max(0, coins.WCoinP + change.WCoinP), Math.Max(0, coins.GoblinPoints + change.GoblinPoints));
            this.Shared = this.Shared with { Coins = updated };
            return ValueTask.FromResult<CoinBalances?>(updated);
        }
    }
}
