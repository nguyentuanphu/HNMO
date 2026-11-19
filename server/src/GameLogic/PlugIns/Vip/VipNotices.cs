// <copyright file="VipNotices.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using System.Globalization;
using System.Runtime.CompilerServices;

/// <summary>Sends account-owned start and last-day notices once per paid interval in a game session.</summary>
/// <remarks>Notice deduplication is ephemeral UI state. Entitlements and payment proof remain in the existing durable stores.</remarks>
/// <param name="clock">The authoritative clock; injectable for tests.</param>
public sealed class VipNotices(TimeProvider? clock = null)
{
    private static readonly TimeZoneInfo Sydney = HnmoStateStore.ResolveTimeZone(null);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly ConditionalWeakTable<Player, NoticeState> _states = new();

    /// <summary>Checks actual paid proof while the caller owns the player's persistence lock.</summary>
    /// <param name="player">The authenticated live player.</param>
    /// <param name="configuration">The active host settings.</param>
    /// <param name="store">The shared VIP section.</param>
    /// <returns>The notice task; a notice failure never changes the payment result.</returns>
    public async ValueTask ShowOwnedAsync(Player player, VipConfiguration configuration, VipStateStore store)
    {
        if (player.Account is not { } account || player.PlayerState.CurrentState != PlayerState.EnteredWorld || VipWallet.HasUnconfirmed(player.PersistenceContext))
        {
            return;
        }

        try
        {
            var now = this._clock.GetUtcNow().UtcDateTime;
            var wallet = new VipWallet(configuration, store, player.GameContext.Configuration, player.Logger);
            if (await wallet.ReadActiveAsync(player.PersistenceContext, account, now).ConfigureAwait(false) is not { } active)
            {
                this._states.Remove(player);
                return;
            }

            var state = this._states.GetValue(player, _ => new());
            var intent = active.Intent;
            var until = intent.Membership.UntilUtc;
            var name = player.Culture.TwoLetterISOLanguageName == "vi" ? intent.NameVi : intent.NameEn;
            var date = TimeZoneInfo.ConvertTimeFromUtc(until, Sydney).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            if (state.OperationId != intent.OperationId || state.UntilUtc != until)
            {
                await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.VipActiveNotice), name, date).ConfigureAwait(false);
                state.OperationId = intent.OperationId;
                state.UntilUtc = until;
                state.Warned = false;
            }

            if (!state.Warned && until - now <= TimeSpan.FromDays(1))
            {
                await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.VipExpiringNotice), name, date).ConfigureAwait(false);
                state.Warned = true;
            }
        }
        catch (Exception exception)
        {
            player.Logger.LogWarning(exception, "VIP notice could not be shown; the account payment result is unchanged.");
        }
    }

    private sealed class NoticeState
    {
        public Guid OperationId { get; set; }

        public DateTime UntilUtc { get; set; }

        public bool Warned { get; set; }
    }
}
