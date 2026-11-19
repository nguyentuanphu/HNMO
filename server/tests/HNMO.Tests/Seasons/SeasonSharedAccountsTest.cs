// <copyright file="SeasonSharedAccountsTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Seasons;

using System.Threading;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Seasons;
using HNMO.Persistence;
using Moq;

/// <summary>
/// Shared accounts and coins between Season 6.3 and Season 9 (<see cref="SeasonSharedAccounts"/>).
/// </summary>
[TestFixture]
public class SeasonSharedAccountsTest
{
    /// <summary>Starts every test with nothing taken.</summary>
    [SetUp]
    public void SetUp() => SeasonSharedAccounts.Reset();

    /// <summary>Season 6.3 (no store) changes nothing.</summary>
    [Test]
    public async Task SeasonSixDoesNothingAsync()
    {
        var account = new Account { LoginName = "a", WCoinC = 5 };
        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(null, account).ConfigureAwait(false), Is.False);
        Assert.That(await SeasonSharedAccounts.MirrorAsync(null, () => throw new InvalidOperationException(), "a").ConfigureAwait(false), Is.False);
        Assert.That(account.WCoinC, Is.EqualTo(5));
    }

    /// <summary>
    /// At login the season's account takes the shared login and coins; a later save adds only what this season spent
    /// and takes back a top-up made on the website meanwhile.
    /// </summary>
    [Test]
    public async Task CoinsSpentInSeasonNineAndToppedUpOnTheWebsiteBothCountAsync()
    {
        var store = new FakeStore(new SharedAccount("test1", "hash", "code", "a@b.c", AccountState.Normal, "vi", null, new CoinBalances(100, 50, 10)));
        var account = new Account { LoginName = "test1", PasswordHash = "old", WCoinC = 0 };
        var context = new Mock<IPlayerContext>();
        context.Setup(c => c.GetAccountByLoginNameAsync("test1", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Assert.That(await SeasonSharedAccounts.MirrorAsync(store, () => context.Object, "TEST1").ConfigureAwait(false), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(account.PasswordHash, Is.EqualTo("hash"), "the shared password");
            Assert.That(account.EMail, Is.EqualTo("a@b.c"));
            Assert.That(account.LanguageIsoCode, Is.EqualTo("vi"));
            Assert.That(CoinBalances.Of(account), Is.EqualTo(new CoinBalances(100, 50, 10)));
        });

        account.WCoinC -= 30; // bought something in Season 9
        account.GoblinPoints += 2; // earned while playing
        store.Shared = store.Shared with { Coins = store.Shared.Coins with { WCoinP = 70 } }; // website top-up meanwhile

        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false), Is.True);
        Assert.That(store.Shared.Coins, Is.EqualTo(new CoinBalances(70, 70, 12)));
        Assert.That(CoinBalances.Of(account), Is.EqualTo(new CoinBalances(70, 70, 12)));

        // Nothing changed: a save adds nothing.
        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false), Is.True);
        Assert.That(store.Shared.Coins, Is.EqualTo(new CoinBalances(70, 70, 12)));
    }

    /// <summary>A failed save to the shared balances keeps the change for the next save; nothing is counted twice.</summary>
    [Test]
    public async Task AFailedSyncIsRetriedOnceAsync()
    {
        var store = new FakeStore(new SharedAccount("p", string.Empty, string.Empty, string.Empty, AccountState.Normal, null, null, new CoinBalances(10, 0, 0)));
        var account = new Account { LoginName = "p" };
        var context = new Mock<IPlayerContext>();
        context.Setup(c => c.GetAccountByLoginNameAsync("p", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await SeasonSharedAccounts.MirrorAsync(store, () => context.Object, "p").ConfigureAwait(false);

        account.WCoinC -= 4;
        store.Fail = true;
        // The save catches it (PlayerPersistence) and goes on; the change waits.
        Assert.ThrowsAsync<InvalidOperationException>(async () => await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false));
        Assert.That(account.WCoinC, Is.EqualTo(6));
        store.Fail = false;
        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(store, account).ConfigureAwait(false), Is.True);
        Assert.That(store.Shared.Coins.WCoinC, Is.EqualTo(6));
        Assert.That(account.WCoinC, Is.EqualTo(6));
    }

    /// <summary>Bots and template accounts never touch the shared balances.</summary>
    [Test]
    public async Task BotsAreLeftAloneAsync()
    {
        var store = new FakeStore(new SharedAccount("bot", string.Empty, string.Empty, string.Empty, AccountState.Normal, null, null, default));
        Assert.That(await SeasonSharedAccounts.SyncCoinsAsync(store, new Account { LoginName = "bot", IsBot = true, WCoinC = 9 }).ConfigureAwait(false), Is.False);
        Assert.That(store.Calls, Is.Zero);
    }

    private sealed class FakeStore(SharedAccount shared) : ISharedAccountStore
    {
        public SharedAccount Shared { get; set; } = shared;

        public bool Fail { get; set; }

        public int Calls { get; private set; }

        public ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(string.Equals(loginName, this.Shared.LoginName, StringComparison.OrdinalIgnoreCase) ? this.Shared : null);

        public ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default)
        {
            this.Calls++;
            if (this.Fail)
            {
                throw new InvalidOperationException("The shared database is out of reach.");
            }

            var coins = this.Shared.Coins;
            var updated = new CoinBalances(Math.Max(0, coins.WCoinC + change.WCoinC), Math.Max(0, coins.WCoinP + change.WCoinP), Math.Max(0, coins.GoblinPoints + change.GoblinPoints));
            this.Shared = this.Shared with { Coins = updated };
            return ValueTask.FromResult<CoinBalances?>(updated);
        }
    }
}
