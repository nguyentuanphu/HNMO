// <copyright file="WebLoginTicketStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Issues and redeems short-lived, single-use login tickets for browser sessions signed in on the website.
/// Tickets are kept only in memory as SHA-256 hashes and are never written to disk or logs.
/// </summary>
public sealed class WebLoginTicketStore
{
    /// <summary>
    /// How long a newly issued login ticket remains valid.
    /// </summary>
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(60);

    private const int TicketRandomBytes = 24; // 192 bits (>= 128 bits)

    private readonly ConcurrentDictionary<string, TicketEntry> _tickets = new(StringComparer.Ordinal);
    private readonly TimeProvider _time;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebLoginTicketStore"/> class using <see cref="TimeProvider.System"/>.
    /// </summary>
    public WebLoginTicketStore()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebLoginTicketStore"/> class with a custom <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="time">The time provider.</param>
    public WebLoginTicketStore(TimeProvider time)
    {
        this._time = time;
    }

    /// <summary>
    /// Gets the shared in-memory ticket store used by the website API and game server login.
    /// </summary>
    public static WebLoginTicketStore Shared { get; } = new();

    /// <summary>
    /// Issues a new single-use login ticket bound to <paramref name="loginName"/>.
    /// </summary>
    /// <param name="loginName">The account login name.</param>
    /// <returns>The raw URL-safe ticket string and its UTC expiry time.</returns>
    public (string Ticket, DateTimeOffset ExpiresAtUtc) Issue(string loginName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loginName);

        var now = this._time.GetUtcNow();
        this.PruneExpired(now);

        Span<byte> rawBytes = stackalloc byte[TicketRandomBytes];
        RandomNumberGenerator.Fill(rawBytes);
        var ticket = Convert.ToBase64String(rawBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ticket));
        var key = Convert.ToHexString(hash);
        var expiresAt = now.Add(TicketLifetime);
        this._tickets[key] = new TicketEntry(hash, loginName.Trim(), expiresAt);
        return (ticket, expiresAt);
    }

    /// <summary>
    /// Attempts to redeem <paramref name="ticket"/> for <paramref name="loginName"/>.
    /// Each ticket can be redeemed at most once.
    /// </summary>
    /// <param name="loginName">The account login name.</param>
    /// <param name="ticket">The raw ticket string.</param>
    /// <returns><c>true</c> if the ticket existed, matched the account, and was not expired; otherwise <c>false</c>.</returns>
    public bool TryRedeem(string loginName, string ticket)
    {
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(ticket));
        var key = Convert.ToHexString(candidateHash);
        if (!this._tickets.TryRemove(key, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAtUtc <= this._time.GetUtcNow())
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(entry.Hash, candidateHash))
        {
            return false;
        }

        return string.Equals(entry.LoginName, loginName.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void PruneExpired(DateTimeOffset now)
    {
        foreach (var pair in this._tickets)
        {
            if (pair.Value.ExpiresAtUtc <= now)
            {
                this._tickets.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record TicketEntry(byte[] Hash, string LoginName, DateTimeOffset ExpiresAtUtc);
}
