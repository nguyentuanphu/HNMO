// <copyright file="VipGameActions.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.GameLogic.PlugIns.Missions;

using HNMO.GameLogic.PlugIns.UltimateAutoPlay;

/// <summary>Connects authenticated live players to the same wallet as the website, under their persistence lock.</summary>
/// <param name="configuration">The active host configuration; disabled by default.</param>
/// <param name="store">The shared VIP section.</param>
/// <param name="clock">The authoritative clock.</param>
/// <param name="notices">Optional session notice deduplication shared with the active plug-in.</param>
public sealed class VipGameActions(VipConfiguration configuration, VipStateStore store, TimeProvider? clock = null, VipNotices? notices = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Runs an own-account action without accepting account names, balances or prices from the client.</summary>
    /// <param name="player">The authenticated in-world player.</param>
    /// <param name="request">The validated selection.</param>
    /// <returns>The safe shared website/game state, or null when the session or request is invalid.</returns>
    public ValueTask<string?> HandleAsync(Player player, VipGameRequest request)
        => player.RunPersistenceExclusiveAsync<string?>(async () =>
        {
            // Recheck after acquiring the lock: a queued request must not mutate a character which has left.
            if (!request.IsValid || player.Account is not { } account || player.SelectedCharacter is null || player.PlayerState.CurrentState != PlayerState.EnteredWorld)
            {
                return null;
            }

            var context = player.PersistenceContext;
            var now = this._clock.GetUtcNow().UtcDateTime;
            var wallet = new VipWallet(configuration, store, player.GameContext.Configuration, player.Logger);
            string? status = null;
            VipRuleResult? rule = null;
            if (request.Action == "purchase")
            {
                if (await VipPendingCoins.ApplyAsync(context, account, now, () => context.SaveChangesAsync()).ConfigureAwait(false))
                {
                    var result = await wallet.PurchaseAsync(context, account, request.OperationId!.Value, request.LevelId, request.Days, now, () => context.SaveChangesAsync(), request.OfferToken).ConfigureAwait(false);
                    status = result.Status;
                    rule = result.Rule;
                }
                else
                {
                    status = "Pending";
                }
            }
            else if (request.Action == "daily")
            {
                var daily = new VipDailyCoins(player.GameContext.PersistenceContextProvider, player.GameContext.Configuration, wallet, store, player.Logger, MissionCoinGranter.CoinRewardsAllowed(player.GameContext));
                status = (await daily.ClaimAsync(context, account, now).ConfigureAwait(false)).Status;
                if (status is "DailyApplied" or "AlreadyClaimed")
                {
                    // A recorded credit remains owed if applying it fails. Retrying the daily reference never duplicates it.
                    await VipPendingCoins.ApplyAsync(context, account, now, () => context.SaveChangesAsync()).ConfigureAwait(false);
                }
            }

            if (!VipWallet.HasUnconfirmed(context))
            {
                player.RefreshAccountAttributeGrants();
                await UltimateAutoPlayPlugIn.SendPolicyAsync(player).ConfigureAwait(false);
                if (notices is not null)
                {
                    await notices.ShowOwnedAsync(player, configuration, store).ConfigureAwait(false);
                }
            }

            return await VipStateWriter.WriteAsync(configuration, store, wallet, context, account, now, request.OperationId, status, rule).ConfigureAwait(false);
        });
}
