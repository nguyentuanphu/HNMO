// <copyright file="SharedAccounts.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.Persistence;

using System.Threading;
using HNMO.DataModel.Entities;

/// <summary>An account as the shared (Season 6.3) database has it: the login and the coins.</summary>
/// <param name="LoginName">The login name.</param>
/// <param name="PasswordHash">The password hash.</param>
/// <param name="SecurityCode">The security code.</param>
/// <param name="EMail">The e-mail address.</param>
/// <param name="State">The account state (bans).</param>
/// <param name="LanguageIsoCode">The language.</param>
/// <param name="ChatBanUntil">The end of a chat ban.</param>
/// <param name="Coins">The coin balances.</param>
public sealed record SharedAccount(
    string LoginName,
    string PasswordHash,
    string SecurityCode,
    string EMail,
    AccountState State,
    string? LanguageIsoCode,
    DateTime? ChatBanUntil,
    CoinBalances Coins);

/// <summary>The coin balances of an account.</summary>
/// <param name="WCoinC">The WCoin (C).</param>
/// <param name="WCoinP">The WCoin (P).</param>
/// <param name="GoblinPoints">The Goblin points.</param>
public readonly record struct CoinBalances(int WCoinC, int WCoinP, int GoblinPoints)
{
    /// <summary>Gets a value indicating whether all balances are zero.</summary>
    public bool IsZero => this.WCoinC == 0 && this.WCoinP == 0 && this.GoblinPoints == 0;

    /// <summary>The balances of an account entity.</summary>
    /// <param name="account">The account.</param>
    /// <returns>Its balances.</returns>
    public static CoinBalances Of(Account account) => new(account.WCoinC, account.WCoinP, account.GoblinPoints);

    /// <summary>The difference between two balances.</summary>
    /// <param name="left">The later balances.</param>
    /// <param name="right">The earlier balances.</param>
    /// <returns>What changed.</returns>
    public static CoinBalances operator -(CoinBalances left, CoinBalances right) =>
        new(left.WCoinC - right.WCoinC, left.WCoinP - right.WCoinP, left.GoblinPoints - right.GoblinPoints);

    /// <summary>Writes the balances into an account entity.</summary>
    /// <param name="account">The account.</param>
    public void ApplyTo(Account account)
    {
        account.WCoinC = this.WCoinC;
        account.WCoinP = this.WCoinP;
        account.GoblinPoints = this.GoblinPoints;
    }
}

/// <summary>
/// The accounts and coins every season shares, kept in the shared (Season 6.3) database. A Season 9 server program
/// reads logins from it and adds its coin changes to it, so one sign-up plays both seasons and coins work in either.
/// </summary>
public interface ISharedAccountStore
{
    /// <summary>Finds an account by its login name.</summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account, or <c>null</c>.</returns>
    ValueTask<SharedAccount?> FindAsync(string loginName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a change to an account's coins in one step and returns the new balances. A balance never goes below zero.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new balances, or <c>null</c> when the account doesn't exist.</returns>
    ValueTask<CoinBalances?> AddCoinsAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a change like <see cref="AddCoinsAsync"/> and returns the balances before and after it, so a caller can take
    /// back exactly what was applied: a balance kept at zero takes less than the change asked for. A store which can do
    /// both in one step overrides this; the default reads the balances first.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The balances before and after, or <c>null</c> when the account doesn't exist.</returns>
    async ValueTask<(CoinBalances Before, CoinBalances After)?> AddCoinsAndReportAsync(string loginName, CoinBalances change, CancellationToken cancellationToken = default)
    {
        if (await this.FindAsync(loginName, cancellationToken).ConfigureAwait(false) is not { } before
            || await this.AddCoinsAsync(loginName, change, cancellationToken).ConfigureAwait(false) is not { } after)
        {
            return null;
        }

        return (before.Coins, after);
    }
}

/// <summary>The shared account store of this program; <c>null</c> in a Season 6.3 program, which owns the accounts.</summary>
public static class SharedAccountStore
{
    /// <summary>Gets or sets the store (set at start-up by a Season 9 program).</summary>
    public static ISharedAccountStore? Current { get; set; }
}
