// <copyright file="SeasonSharedAccounts.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Seasons;

using System.Collections.Concurrent;
using System.Threading;
using HNMO.Persistence;

/// <summary>
/// Shared accounts and coins in a Season 9 program (owner's decision, 2026-11-19): one sign-up plays both seasons and
/// coins work in either. The shared (Season 6.3) database owns the logins and the coins:
/// <list type="bullet">
/// <item>at login, the account's login, state and coins are copied into this season's database (made there on its
/// first login), so the normal login checks the shared password;</item>
/// <item>at every progress save, what this season changed in the coins is added to the shared balances in one step,
/// and the account takes the shared balances back (with any top-up made on the website meanwhile).</item>
/// </list>
/// Characters, vaults and items stay in the season's own database. Without a store (Season 6.3) nothing happens.
/// </summary>
public static class SeasonSharedAccounts
{
    /// <summary>The shared balances each account had when this season last took them, by login name.</summary>
    private static readonly ConcurrentDictionary<string, CoinBalances> Baselines = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Serializes coin syncs per login name so concurrent saves cannot double-apply an in-flight delta.</summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SyncLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Copies a shared account into this season's database before the login checks it.
    /// </summary>
    /// <param name="store">The shared store, or <c>null</c> (Season 6.3).</param>
    /// <param name="createContext">Makes a player context of this season's database.</param>
    /// <param name="loginName">The login name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if a shared account was copied.</returns>
    public static async ValueTask<bool> MirrorAsync(ISharedAccountStore? store, Func<IPlayerContext> createContext, string loginName, CancellationToken cancellationToken = default)
    {
        if (store is null || string.IsNullOrWhiteSpace(loginName))
        {
            return false;
        }

        var shared = await store.FindAsync(loginName.Trim(), cancellationToken).ConfigureAwait(false);
        if (shared is null)
        {
            return false;
        }

        using var context = createContext();
        var account = await context.GetAccountByLoginNameAsync(shared.LoginName).ConfigureAwait(false);
        if (account is null)
        {
            account = context.CreateNew<Account>();
            account.LoginName = shared.LoginName;
            account.Vault = context.CreateNew<ItemStorage>(); // each season has its own vault (owner's decision)
            account.RegistrationDate = DateTime.UtcNow;
        }

        account.PasswordHash = shared.PasswordHash;
        account.SecurityCode = shared.SecurityCode;
        account.EMail = shared.EMail;
        account.State = shared.State;
        account.LanguageIsoCode = shared.LanguageIsoCode ?? account.LanguageIsoCode;
        account.ChatBanUntil = shared.ChatBanUntil;
        shared.Coins.ApplyTo(account);
        if (!await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        Baselines[shared.LoginName] = shared.Coins;
        return true;
    }

    /// <summary>
    /// Adds this season's coin changes to the shared balances and takes the shared balances back into the account.
    /// Called while the player's progress is saved; a failure leaves the change for the next save.
    /// </summary>
    /// <param name="store">The shared store, or <c>null</c> (Season 6.3).</param>
    /// <param name="account">The account of the player being saved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> if the shared balances were updated.</returns>
    public static async ValueTask<bool> SyncCoinsAsync(ISharedAccountStore? store, Account? account, CancellationToken cancellationToken = default)
        => await TrySyncCoinsAsync(store, account, cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>
    /// Like <see cref="SyncCoinsAsync"/>, but returns what the sync did, so a caller whose own save fails afterwards can
    /// take it back with <see cref="UndoCoinSyncAsync"/> (the shared store must not keep a fee whose benefit was not saved).
    /// </summary>
    /// <param name="store">The shared store, or <c>null</c> (Season 6.3).</param>
    /// <param name="account">The account of the player being saved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The receipt, or <c>null</c> if the shared balances were not updated.</returns>
    public static async ValueTask<CoinSyncReceipt?> TrySyncCoinsAsync(ISharedAccountStore? store, Account? account, CancellationToken cancellationToken = default)
    {
        if (store is null || account is null || account.IsBot || account.IsTemplate || string.IsNullOrEmpty(account.LoginName))
        {
            return null;
        }

        var syncLock = SyncLocks.GetOrAdd(account.LoginName, static _ => new SemaphoreSlim(1, 1));
        await syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = CoinBalances.Of(account);
            var baseline = Baselines.TryGetValue(account.LoginName, out var known) ? known : snapshot;
            var delta = snapshot - baseline;
            var result = await store.AddCoinsAndReportAsync(account.LoginName, delta, cancellationToken).ConfigureAwait(false);
            if (result is not { } change)
            {
                return null;
            }

            var shared = change.After;

            // A balance kept at zero takes less than asked: the receipt holds what the shared store really took, so an
            // undo gives back no more than that.
            var applied = change.After - change.Before;

            var inFlightDelta = CoinBalances.Of(account) - snapshot;
            new CoinBalances(
                shared.WCoinC + inFlightDelta.WCoinC,
                shared.WCoinP + inFlightDelta.WCoinP,
                shared.GoblinPoints + inFlightDelta.GoblinPoints).ApplyTo(account);
            Baselines[account.LoginName] = shared;
            return new CoinSyncReceipt(account.LoginName, applied, baseline, shared - snapshot);
        }
        finally
        {
            syncLock.Release();
        }
    }

    /// <summary>
    /// Takes back a sync whose caller could not save its own progress: the change goes out of the shared store again, the
    /// account loses what the sync took over from the shared balances, and the baseline goes back, so the next sync sends
    /// the change again (once).
    /// </summary>
    /// <param name="store">The shared store.</param>
    /// <param name="account">The account that was synced.</param>
    /// <param name="receipt">What the sync did.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public static async ValueTask UndoCoinSyncAsync(ISharedAccountStore store, Account account, CoinSyncReceipt receipt, CancellationToken cancellationToken = default)
    {
        var syncLock = SyncLocks.GetOrAdd(receipt.LoginName, static _ => new SemaphoreSlim(1, 1));
        await syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (receipt.Delta != default)
            {
                var back = new CoinBalances(-receipt.Delta.WCoinC, -receipt.Delta.WCoinP, -receipt.Delta.GoblinPoints);
                if (await store.AddCoinsAsync(receipt.LoginName, back, cancellationToken).ConfigureAwait(false) is null)
                {
                    return;
                }
            }

            (CoinBalances.Of(account) - receipt.Shift).ApplyTo(account);
            Baselines[receipt.LoginName] = receipt.PreviousBaseline;
        }
        finally
        {
            syncLock.Release();
        }
    }

    /// <summary>Forgets what was taken (tests).</summary>
    internal static void Reset() => Baselines.Clear();
}

/// <summary>What one <see cref="SeasonSharedAccounts.TrySyncCoinsAsync"/> did, to take it back if the caller's save fails.</summary>
/// <param name="LoginName">The login name.</param>
/// <param name="Delta">The change the shared store really applied (less than asked when a balance was kept at zero).</param>
/// <param name="PreviousBaseline">The baseline before the sync.</param>
/// <param name="Shift">What the account took over from the shared balances (shared balance after the change minus the account's own balance).</param>
public readonly record struct CoinSyncReceipt(string LoginName, CoinBalances Delta, CoinBalances PreviousBaseline, CoinBalances Shift);
