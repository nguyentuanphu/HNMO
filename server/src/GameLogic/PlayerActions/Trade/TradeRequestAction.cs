// <copyright file="TradeRequestAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Trade;

using HNMO.GameLogic.Views.Trade;

/// <summary>
/// Action to request a trade with another player.
/// </summary>
public class TradeRequestAction
{
    /// <summary>
    /// Requests the trade from the other player.
    /// </summary>
    /// <param name="player">The player who requests the trade.</param>
    /// <param name="partner">The partner which will be requested.</param>
    /// <returns>The success of sending the request to the <paramref name="partner"/>.</returns>
    public async ValueTask<bool> RequestTradeAsync(ITrader player, ITrader partner)
    {
        if (player is Player arenaPlayer && await PlugIns.Arena.ArenaRules.RefuseAsync(arenaPlayer, PlugIns.Arena.ArenaRules.Action.Trade).ConfigureAwait(false))
        {
            return false;
        }

        if (player.IsTemplatePlayer || partner.IsTemplatePlayer)
        {
            player.Logger.LogWarning("This or the requested player is template player, cannot trade.");
            return false;
        }

        if (partner is Player targetPlayer && PlugIns.Qol.QolPlugIn.AreRequestsBlocked(targetPlayer))
        {
            if (player is Player requesterPlayer)
            {
                await requesterPlayer.ShowBlueMessageAsync(requesterPlayer.Culture.TwoLetterISOLanguageName == "vi"
                    ? $"{partner.Name} đang tắt nhận lời mời (/requests off)."
                    : $"{partner.Name} has disabled incoming requests (/requests off).").ConfigureAwait(false);
            }

            return false;
        }

        // HNMO's account safety: both traders need their second password (when the admin requires it).
        if (PlugIns.AccountSafety.AccountSafetyPlugIn.Get(player.GameContext) is { } safety)
        {
            if (player is Player requester && await safety.RefuseAsync(requester, PlugIns.AccountSafety.SafetyAction.Trade).ConfigureAwait(false))
            {
                return false;
            }

            if (partner is Player requested && await safety.RefuseAsync(requested, PlugIns.AccountSafety.SafetyAction.Trade).ConfigureAwait(false))
            {
                if (player is Player requesterPlayer)
                {
                    await requesterPlayer.ShowBlueMessageAsync(requesterPlayer.Culture.TwoLetterISOLanguageName == "vi"
                        ? $"{partner.Name} cần nhập mật khẩu cấp 2 trước khi giao dịch."
                        : $"{partner.Name} has to enter their second password before trading.").ConfigureAwait(false);
                }

                return false;
            }
        }

        if (player.ViewPlugIns.GetPlugIn<IShowTradeRequestPlugIn>() is null || partner.ViewPlugIns.GetPlugIn<IShowTradeRequestAnswerPlugIn>() is null)
        {
            return false;
        }

        if (!await partner.PlayerState.TryAdvanceToAsync(PlayerState.TradeRequested).ConfigureAwait(false))
        {
            return false;
        }

        if (!await player.PlayerState.TryAdvanceToAsync(PlayerState.TradeRequested).ConfigureAwait(false))
        {
            await partner.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
            return false;
        }

        player.TradingPartner = partner;
        partner.TradingPartner = player;
        await partner.InvokeViewPlugInAsync<IShowTradeRequestPlugIn>(p => p.ShowTradeRequestAsync(player)).ConfigureAwait(false);
        return true;
    }
}