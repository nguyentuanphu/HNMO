// <copyright file="SharedLoginTickets.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence;

using System.Threading;

/// <summary>
/// The website's one-time login tickets where every season's server program can redeem them: the website's "Play"
/// gets a ticket from Season 6.3's program, and the player may pick a Season 9 server. Only the ticket's SHA-256 hash
/// is stored, for its 60 seconds; redeeming removes it, so a ticket works once in either season.
/// </summary>
public interface ISharedLoginTicketStore
{
    /// <summary>Adds a ticket.</summary>
    /// <param name="hash">The ticket's hash (hex).</param>
    /// <param name="loginName">The account it's for.</param>
    /// <param name="expiresAtUtc">When it expires.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task which completes when the ticket is stored.</returns>
    ValueTask AddAsync(string hash, string loginName, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Takes a ticket out, so it can't be used again.</summary>
    /// <param name="hash">The ticket's hash (hex).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ticket's account and expiry, or <c>null</c> when there is no such ticket.</returns>
    ValueTask<(string LoginName, DateTimeOffset ExpiresAtUtc)?> TakeAsync(string hash, CancellationToken cancellationToken = default);
}

/// <summary>The shared login ticket store of this program; <c>null</c> without a database (demo, tests).</summary>
public static class SharedLoginTicketStore
{
    /// <summary>Gets or sets the store (set at start-up).</summary>
    public static ISharedLoginTicketStore? Current { get; set; }
}
