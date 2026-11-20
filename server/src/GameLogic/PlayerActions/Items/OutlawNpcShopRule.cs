// <copyright file="OutlawNpcShopRule.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Items;

/// <summary>
/// As in the Season 6/9 server suites, outlaws can't use the NPC shops: from PK level 2 up they can't open a
/// merchant, buy, sell or repair at an NPC. The Warning stage (one kill) still may.
/// </summary>
public static class OutlawNpcShopRule
{
    /// <summary>
    /// Determines whether the player may not use NPC shops.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c> if the player is an outlaw of PK level 2 or above.</returns>
    public static bool IsRefused(Player player)
    {
        return player.SelectedCharacter?.State >= HeroState.PlayerKiller1stStage;
    }

    /// <summary>
    /// Refuses the NPC shop to an outlaw of PK level 2 or above, with a message.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c> if the shop is refused.</returns>
    public static async ValueTask<bool> RefuseAsync(Player player)
    {
        if (!IsRefused(player))
        {
            return false;
        }

        await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.PlayerKillersCantUseNpcShops)).ConfigureAwait(false);
        return true;
    }
}
