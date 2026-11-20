// <copyright file="TradeMoneyAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Trade;

using HNMO.GameLogic.Views.Inventory;
using HNMO.GameLogic.Views.Trade;

/// <summary>
/// Action to set the traded money.
/// </summary>
public class TradeMoneyAction
{
    /// <summary>
    /// Sets the money which should be traded to the other player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="moneyAmount">The money amount.</param>
    public async ValueTask TradeMoneyAsync(Player player, uint moneyAmount)
    {
        // Check if Trade is open
        if (player.PlayerState.CurrentState != PlayerState.TradeOpened)
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.UncheckTradeAcceptButtonFirst)).ConfigureAwait(false);
            return;
        }

        // The zen which is already offered was taken out of the inventory, but it still belongs to the
        // player: the new amount replaces it. So it counts to the available zen, otherwise the offer
        // couldn't be raised (or even lowered) with the zen which is already in the trade.
        var availableMoney = (long)player.Money + player.TradingMoney;
        if (availableMoney < moneyAmount)
        {
            return;
        }

        // Add the Money to the Trade
        player.Money = (int)(availableMoney - moneyAmount);
        player.TradingMoney = (int)moneyAmount;
        await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IRequestedTradeMoneyHasBeenSetPlugIn>(p => p.RequestedTradeMoneyHasBeenSetAsync()).ConfigureAwait(false);

        await player.InvokeViewPlugInAsync<IChangeTradeButtonStatePlugIn>(p => p.ChangeTradeButtonStateAsync(TradeButtonState.Red)).ConfigureAwait(false);
        if (player.TradingPartner is { } tradingPartner)
        {
            // Send the Money Packet to the Trading Partner
            await tradingPartner.InvokeViewPlugInAsync<ISetTradeMoneyPlugIn>(p => p.SetTradeMoneyAsync(moneyAmount)).ConfigureAwait(false);
            await tradingPartner.PlayerState.TryAdvanceToAsync(PlayerState.TradeOpened).ConfigureAwait(false);
            await tradingPartner.InvokeViewPlugInAsync<IChangeTradeButtonStatePlugIn>(p => p.ChangeTradeButtonStateAsync(TradeButtonState.Red)).ConfigureAwait(false);
        }
    }
}