// <copyright file="HnmoWarpGuard.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlayerActions;

/// <summary>
/// The state checks of the warps (M menu, /move, gates, spouse warp, Teleport NPC) (audits 1.10 and 12):
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

        if (player.DuelRoom is { State: DuelState.DuelStarted })
        {
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

    /// <summary>Gets the text of a refusal in the language of the player.</summary>
    /// <param name="player">The player.</param>
    /// <param name="refusal">The refusal.</param>
    /// <returns>The text.</returns>
    public static string TextFor(Player player, (string En, string Vi) refusal) =>
        string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase) ? refusal.Vi : refusal.En;
}
