// <copyright file="OpenStoreAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.PlayerStore;

using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlayerShopCurrencies;
using HNMO.GameLogic.Views.PlayerShop;

/// <summary>
/// Action to open a player store.
/// </summary>
public class OpenStoreAction
{
    /// <summary>
    /// The level a character needs to open a personal store, the same as to set the prices (<see cref="SetItemPriceAction"/>).
    /// </summary>
    internal const int MinimumLevel = 6;

    /// <summary>
    /// Opens the player store.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="storeName">Name of the store.</param>
    public async ValueTask OpenStoreAsync(Player player, string storeName)
    {
        if (await PlugIns.Arena.ArenaRules.RefuseAsync(player, PlugIns.Arena.ArenaRules.Action.Store).ConfigureAwait(false))
        {
            return;
        }

        using var loggerScope = player.Logger.BeginScope(this.GetType());
        var character = player.SelectedCharacter;
        if (character is null)
        {
            player.Logger.LogWarning("OpenStore request failed: No character selected.");
            return;
        }

        if (player.Level < MinimumLevel)
        {
            player.Logger.LogWarning("OpenStore request failed: Character level below {0}. Player: [{1}]", MinimumLevel, character.Name);
            return;
        }

        if (IsBusy(player))
        {
            player.Logger.LogWarning("OpenStore request failed: The player is trading, in an NPC dialog, dead or duelling. Player: [{0}], State: {1}", character.Name, player.PlayerState.CurrentState);
            return;
        }

        if (player.ShopStorage?.Items.Any(i => i.GetStorePrice() is null) ?? true)
        {
            player.Logger.LogWarning("OpenStore request failed: Not all store items have a price assigned. Player: [{0}], StoreName: [{1}]", character.Name, character.StoreName);
            return;
        }

        if (player.ShopStorage?.Items.Any(i => i.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.HarmonyOption)) ?? true)
        {
            player.Logger.LogWarning("OpenStore request failed: Items with an harmony option can't be traded. Player: [{0}], StoreName: [{1}]", character.Name, character.StoreName);
            return;
        }

        if (player.ShopStorage.Items.FirstOrDefault(i => i.GetStorePrice() is { } price && !ShopPrices.IsAllowed(player.GameContext, price)) is { } itemWithDisallowedPrice)
        {
            var price = itemWithDisallowedPrice.GetStorePrice()!.Value;
            player.Logger.LogWarning("OpenStore request failed: The price of item {0} isn't allowed. Player: [{1}], StoreName: [{2}]", itemWithDisallowedPrice, character.Name, character.StoreName);
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.PlayerShopCurrencyNotAllowed), player.GetCurrencyName(price)).ConfigureAwait(false);
            return;
        }

        character.StoreName = storeName;
        player.ShopStorage.StoreOpen = true;
        player.Logger.LogDebug("OpenStore: Player: [{0}], StoreName: [{1}]", character.Name, character.StoreName);
        await player.ForEachWorldObserverAsync<IPlayerShopOpenedPlugIn>(p => p.PlayerShopOpenedAsync(player), true).ConfigureAwait(false);
    }

    /// <summary>
    /// Restores the store's open state after the player entered the game.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="isStoreOpeningAfterEnterSupported">
    /// Whether re-opening the store to other players after entering the game is supported by the player's client.
    /// </param>
    /// <remarks>
    /// If the store was left open before logging out, its name is set, and the client supports it,
    /// the store is properly re-opened and announced to nearby players (and the owner) via
    /// <see cref="OpenStoreAsync"/>. Otherwise, the store is (re-)marked as closed for everyone,
    /// including the owner - it must never appear open only to the owner while staying closed to
    /// everyone else.
    /// </remarks>
    public async ValueTask RestoreAfterEnterWorldAsync(Player player, bool isStoreOpeningAfterEnterSupported)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        if (character.IsStoreOpened
            && !string.IsNullOrWhiteSpace(character.StoreName)
            && isStoreOpeningAfterEnterSupported)
        {
            character.IsStoreOpened = false;
            await this.OpenStoreAsync(player, character.StoreName).ConfigureAwait(false);
            if (player.ShopStorage?.StoreOpen ?? false)
            {
                // The client forgot the prices of the store items when the player logged out; it
                // restores them from the item list.
                await player.InvokeViewPlugInAsync<IShowShopItemListPlugIn>(p => p.ShowShopItemListAsync(player, true)).ConfigureAwait(false);
            }
        }
        else
        {
            character.IsStoreOpened = false;
        }
    }

    /// <summary>
    /// Determines whether the player is busy with something else (owner, 21 Nov 2026): trading, in an NPC dialog,
    /// dead or duelling.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c>, if the store can't be opened now.</returns>
    internal static bool IsBusy(Player player)
    {
        var state = player.PlayerState.CurrentState;
        return !player.IsAlive
               || player.DuelRoom is not null
               || state == PlayerState.TradeRequested
               || state == PlayerState.TradeOpened
               || state == PlayerState.TradeButtonPressed
               || state == PlayerState.NpcDialogOpened;
    }

}