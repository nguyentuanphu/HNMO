// <copyright file="WebsiteEmailChallenge.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>One expiring, password-bound mailbox challenge; the random code is never persisted.</summary>
/// <param name="ProtectedEmail">The encrypted new email address.</param>
/// <param name="CodeHash">The salted code hash.</param>
/// <param name="Salt">The random hash salt.</param>
/// <param name="PasswordHash">The fingerprint of the password hash which authorized the request.</param>
/// <param name="OldEmailHash">The previous email fingerprint.</param>
/// <param name="ExpiresAtUtc">The confirmation deadline.</param>
/// <param name="NextSendAtUtc">The resend deadline, retained on failed delivery.</param>
/// <param name="Attempts">The number of incorrect confirmations.</param>
/// <param name="State">Sending, pending, applying, consumed or failed.</param>
public sealed record WebsiteEmailChallenge(string ProtectedEmail, string CodeHash, string Salt, string PasswordHash, string OldEmailHash,
    DateTime ExpiresAtUtc, DateTime NextSendAtUtc, int Attempts, string State);
