// <copyright file="SharedLoginTicketTest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests.Seasons;

using System.Collections.Concurrent;
using System.Threading;
using HNMO.GameLogic.PlayerActions;
using HNMO.Persistence;

/// <summary>
/// The website's "Play" ticket works for a server of either season, once (<see cref="WebLoginTicketStore"/> with a
/// <see cref="ISharedLoginTicketStore"/>).
/// </summary>
[TestFixture]
public class SharedLoginTicketTest
{
    /// <summary>A ticket issued by Season 6.3's program is redeemed once by Season 9's, and never again anywhere.</summary>
    [Test]
    public async Task ATicketWorksOnceInEitherSeasonAsync()
    {
        var shared = new FakeTickets();
        var seasonSix = new WebLoginTicketStore();
        var seasonNine = new WebLoginTicketStore();
        var (ticket, _) = await seasonSix.IssueAsync("test1", shared).ConfigureAwait(false);
        Assert.That(shared.Rows, Has.Count.EqualTo(1));
        Assert.That(shared.Rows.Keys.Single(), Does.Not.Contain(ticket), "only the hash is stored");

        Assert.That(await seasonNine.TryRedeemAsync("TEST1", ticket, shared).ConfigureAwait(false), Is.True);
        Assert.That(await seasonNine.TryRedeemAsync("test1", ticket, shared).ConfigureAwait(false), Is.False, "used");
        Assert.That(await seasonSix.TryRedeemAsync("test1", ticket, shared).ConfigureAwait(false), Is.False, "used in the other season");
        Assert.That(seasonSix.TryRedeem("test1", ticket), Is.False, "the issuing program's own copy is gone");
    }

    /// <summary>A ticket for another account, or an expired one, is refused.</summary>
    [Test]
    public async Task WrongAccountOrExpiredIsRefusedAsync()
    {
        var shared = new FakeTickets();
        var store = new WebLoginTicketStore();
        var (ticket, _) = await store.IssueAsync("test1", shared).ConfigureAwait(false);
        Assert.That(await store.TryRedeemAsync("test2", ticket, shared).ConfigureAwait(false), Is.False);

        var (old, _) = await store.IssueAsync("test1", shared).ConfigureAwait(false);
        var key = shared.Rows.Keys.Single();
        shared.Rows[key] = (shared.Rows[key].LoginName, DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.That(await store.TryRedeemAsync("test1", old, shared).ConfigureAwait(false), Is.False);
    }

    private sealed class FakeTickets : ISharedLoginTicketStore
    {
        public ConcurrentDictionary<string, (string LoginName, DateTimeOffset ExpiresAtUtc)> Rows { get; } = new();

        public ValueTask AddAsync(string hash, string loginName, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
        {
            this.Rows[hash] = (loginName, expiresAtUtc);
            return ValueTask.CompletedTask;
        }

        public ValueTask<(string LoginName, DateTimeOffset ExpiresAtUtc)?> TakeAsync(string hash, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Rows.TryRemove(hash, out var row) ? row : ((string, DateTimeOffset)?)null);
    }
}
