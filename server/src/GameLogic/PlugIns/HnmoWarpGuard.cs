// <copyright file="HnmoWarpGuard.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlayerActions;

/// <summary>
/// The state checks of HNMO's own warps (spouse warp, Teleport NPC), as the game's warps have them (audit 1.10):
/// alive, not in a duel, no trade, and the target map's own rules.
/// </summary>
public static class HnmoWarpGuard
{
    /// <summary>Why the player can't warp now, in English and Vietnamese; <c>null</c> when it can.</summary>
    /// <param name="player">The player.</param>
    /// <param name="target">The map it would warp to.</param>
    /// <returns>The refusal, or <c>null</c>.</returns>
    public static (string En, string Vi)? Refusal(Player player, GameMapDefinition target)
    {
        if (!player.IsAlive)
        {
            return ("You can't warp while dead.", "Không thể dịch chuyển khi đã chết.");
        }

        if (player.DuelRoom is { State: DuelState.DuelStarted } duelRoom && duelRoom.IsDuelist(player))
        {
            // Spectators may leave; their room removes them when they leave the duel map.
            return ("You can't warp during a duel.", "Không thể dịch chuyển khi đang đấu tay đôi.");
        }

        var state = player.PlayerState.CurrentState;
        if (state == PlayerState.TradeRequested || state == PlayerState.TradeOpened || state == PlayerState.TradeButtonPressed)
        {
            return ("Close the trade before you warp.", "Hãy đóng giao dịch trước khi dịch chuyển.");
        }

        if (target.TryGetRequirementError(player, out var message))
        {
            return (message, message);
        }

        return null;
    }
}
