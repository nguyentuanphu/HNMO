// <copyright file="WebsiteEmailChallengeStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HNMO.DataModel.Entities;
using HNMO.Persistence;
using Microsoft.AspNetCore.DataProtection;

/// <summary>Uses the existing player-row backend; caller holds the account login reservation across every mutation.</summary>
/// <param name="rows">The existing PostgreSQL feature-row backend.</param>
/// <param name="protection">The persisted server key ring.</param>
public sealed class WebsiteEmailChallengeStore(IHnmoPlayerStateRowBackend rows, IDataProtectionProvider protection)
{
    private readonly IDataProtector _protector = protection.CreateProtector("HNMO.Website.EmailChallenge.v1");

    /// <summary>Reads only this account's indexed feature key, not every player's challenges.</summary>
    /// <param name="id">The authenticated account id.</param>
    /// <returns>The current challenge, if any.</returns>
    public WebsiteEmailChallenge? Read(Guid id)
    {
        var row = rows.LoadAll(Feature(id)).SingleOrDefault();
        return row is null ? null : JsonSerializer.Deserialize<WebsiteEmailChallenge>(row.PayloadJson);
    }

    /// <summary>Creates and persists a challenge before attempting delivery.</summary>
    /// <param name="account">The freshly authenticated account under its reservation.</param>
    /// <param name="email">The validated new mailbox.</param>
    /// <param name="nowUtc">The current UTC instant.</param>
    /// <returns>The pending state and transient code, which must go only to the mailbox.</returns>
    public (WebsiteEmailChallenge Challenge, string Code) Create(Account account, string email, DateTime nowUtc)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var challenge = new WebsiteEmailChallenge(this._protector.Protect(email), Fingerprint(salt + code), salt,
            Fingerprint(account.PasswordHash), Fingerprint(account.EMail), nowUtc.AddMinutes(15), nowUtc.AddMinutes(1), 0, "sending");
        this.Write(account.GetId(), challenge, nowUtc);
        return (challenge, code);
    }

    /// <summary>Writes one state transition atomically through the existing row backend.</summary>
    /// <param name="id">The authenticated account id.</param>
    /// <param name="challenge">The next state.</param>
    /// <param name="nowUtc">The current UTC instant.</param>
    public void Write(Guid id, WebsiteEmailChallenge challenge, DateTime nowUtc)
        => rows.UpsertBatch([new(Feature(id), id.ToString("N"), JsonSerializer.Serialize(challenge), nowUtc, challenge.ExpiresAtUtc)], []);

    /// <summary>Validates the code and its authorizing account state without changing persistence.</summary>
    /// <param name="account">The freshly authenticated account under its reservation.</param>
    /// <param name="challenge">The current state.</param>
    /// <param name="code">The player's code.</param>
    /// <param name="nowUtc">The current UTC instant.</param>
    /// <returns>The verified new address, or null.</returns>
    public string? Verify(Account account, WebsiteEmailChallenge challenge, string code, DateTime nowUtc)
    {
        if (challenge.State is not ("pending" or "applying") || challenge.Attempts >= 5 || nowUtc >= challenge.ExpiresAtUtc
            || code.Length != 24 || Fingerprint(account.PasswordHash) != challenge.PasswordHash
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Fingerprint(challenge.Salt + code.ToUpperInvariant())), Encoding.ASCII.GetBytes(challenge.CodeHash)))
        {
            return null;
        }

        var email = this._protector.Unprotect(challenge.ProtectedEmail);
        var original = Fingerprint(account.EMail) == challenge.OldEmailHash;
        var recovered = challenge.State == "applying" && account.EMail == email;
        return (original || recovered) && WebsiteMailSettingsStore.IsAddress(email) ? email : null;
    }

    private static string Feature(Guid id) => $"website.email.{id:N}";

    private static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
