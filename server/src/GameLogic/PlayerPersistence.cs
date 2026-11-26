// <copyright file="PlayerPersistence.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using System.Threading;
using HNMO.Persistence;
using Nito.AsyncEx;

/// <summary>
/// Serializes the context mutations of a <see cref="Player"/> against its progress saves.
/// </summary>
/// <remarks>
/// The periodic progress save (<see cref="PlugIns.PeriodicSaveProgressPlugIn"/>) runs on an
/// independent timer flow. Action handlers mutate tracked entities with plain field/collection
/// writes (e.g. crafting toggling <c>item.ItemOptions</c>) which bypass the persistence context's
/// own lock; if such a mutation runs while <see cref="IContext.SaveChangesAsync"/> enumerates the
/// change tracker, the save throws (collection-modified / DbUpdateConcurrency) and every following
/// save fails too, so the whole session is lost on relog. Serializing the packet handler funnel
/// and the save against each other closes that window. The lock is re-entrant per asynchronous
/// flow, so an inline save inside an already-serialized handler does not deadlock.
/// <para>
/// Invariant: never acquire another player's persistence lock (via their
/// <see cref="Player.SaveProgressAsync"/> or <see cref="RunExclusiveAsync{T}"/>) from inside a
/// packet handler, which already holds this player's lock, unless a global lock order is enforced.
/// Today only the trade accept does a cross-player save, and it cannot form a cycle because a trade
/// has a single accepting side (so the A-then-B acquisition order has no concurrent B-then-A
/// counterpart). A second cross-player caller with the opposite order could deadlock.
/// </para>
/// </remarks>
internal sealed class PlayerPersistence
{
    private readonly Player _player;

    private readonly AsyncLock _lock = new();

    /// <summary>
    /// Tracks, per asynchronous flow, whether <see cref="_lock"/> is already held, so the
    /// lock can be re-entered (Nito's <see cref="AsyncLock"/> is not reentrant). It is an instance
    /// field on purpose: reentrancy must be tracked per player, so a flow holding player A's lock
    /// still acquires player B's lock (e.g. during a trade) instead of wrongly skipping it.
    /// </summary>
    private readonly AsyncLocal<bool> _lockHeld = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerPersistence"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public PlayerPersistence(Player player)
    {
        this._player = player;
    }

    /// <summary>
    /// Saves the progress of the player.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success of the save operation.</returns>
    public async ValueTask<bool> SaveProgressAsync(CancellationToken cancellationToken = default)
    {
        if (this._player.IsTemplatePlayer)
        {
            return true;
        }

        return await this.RunExclusiveAsync(
            async () =>
            {
                var receipt = await this.SyncSharedCoinsAsync(cancellationToken).ConfigureAwait(false);
                var saved = false;
                try
                {
                    saved = await this._player.PersistenceContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return saved;
                }
                finally
                {
                    if (!saved && receipt is { } done)
                    {
                        await this.UndoSharedCoinsAsync(done).ConfigureAwait(false);
                    }
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    // A Season 9 program's coin changes go to the shared balances (Seasons.SeasonSharedAccounts); never blocks the save.
    // The receipt lets the save take them back when the local save fails: the shared store must not keep a fee whose
    // benefit was not saved with it.
    private async ValueTask<Seasons.CoinSyncReceipt?> SyncSharedCoinsAsync(CancellationToken cancellationToken)
    {
        if (HNMO.Persistence.SharedAccountStore.Current is not { } store)
        {
            return null;
        }

        try
        {
            return await Seasons.SeasonSharedAccounts.TrySyncCoinsAsync(store, this._player.Account, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this._player.Logger.LogWarning(ex, "Couldn't add the coin changes of [{Account}] to the shared balances; the next save tries again.", this._player.Account?.LoginName);
            return null;
        }
    }

    private async ValueTask UndoSharedCoinsAsync(Seasons.CoinSyncReceipt receipt)
    {
        if (HNMO.Persistence.SharedAccountStore.Current is not { } store || this._player.Account is not { } account)
        {
            return;
        }

        try
        {
            await Seasons.SeasonSharedAccounts.UndoCoinSyncAsync(store, account, receipt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._player.Logger.LogWarning(ex, "Couldn't take the coin changes of [{Account}] back from the shared balances after a failed save.", account.LoginName);
        }
    }

    /// <summary>
    /// Runs the given operation while holding this player's persistence lock, so that context
    /// mutations and progress saves for the player never run concurrently.
    /// </summary>
    /// <typeparam name="T">The result type of the operation.</typeparam>
    /// <param name="operation">The operation to run exclusively.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public async ValueTask<T> RunExclusiveAsync<T>(Func<ValueTask<T>> operation, CancellationToken cancellationToken = default)
    {
        if (this._lockHeld.Value)
        {
            return await operation().ConfigureAwait(false);
        }

        using var l = await this._lock.LockAsync(cancellationToken).ConfigureAwait(false);
        this._lockHeld.Value = true;
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            this._lockHeld.Value = false;
        }
    }

    /// <summary>
    /// Runs the given operation while holding this player's persistence lock.
    /// </summary>
    /// <param name="operation">The operation to run exclusively.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A value task which completes when the operation completed.</returns>
    public async ValueTask RunExclusiveAsync(Func<ValueTask> operation, CancellationToken cancellationToken = default)
    {
        if (this._lockHeld.Value)
        {
            await operation().ConfigureAwait(false);
            return;
        }

        using var l = await this._lock.LockAsync(cancellationToken).ConfigureAwait(false);
        this._lockHeld.Value = true;
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            this._lockHeld.Value = false;
        }
    }
}
