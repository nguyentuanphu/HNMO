// <copyright file="StoreItemListRequestAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.PlayerStore;

using HNMO.GameLogic.PlayerShopCurrencies;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.PlayerShop;
using HNMO.Interfaces;

/// <summary>
/// Action to request the item list of a player store.
/// </summary>
public class StoreItemListRequestAction
{
    /// <summary>
    /// Requests the store item list.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requestedPlayer">The requested player.</param>
    public async ValueTask RequestStoreItemListAsync(Player player, Player requestedPlayer)
    {
        if (!(requestedPlayer.ShopStorage?.StoreOpen ?? false))
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.PlayerStoreNotOpen)).ConfigureAwait(false);
            return;
        }

        player.LastRequestedPlayerStore = new WeakReference<Player>(requestedPlayer);
        await player.ShowAllowedPricesAsync().ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IShowShopItemListPlugIn>(p => p.ShowShopItemListAsync(requestedPlayer, false)).ConfigureAwait(false);
    }
}