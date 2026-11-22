// <copyright file="BotMiniGameHandler.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Bots;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.MiniGames;
using HNMO.GameLogic.Offline;

/// <summary>
/// Prevents server-side bots from entering mini game events (Blood Castle, Devil Square, Chaos
/// Castle): bots stay outside in the regular world maps instead of occupying event slots or
/// distorting event rankings.
/// </summary>
internal static class BotMiniGameHandler
{
    /// <summary>
    /// Returns an empty list because bots do not follow party leaders into mini game events.
    /// </summary>
    /// <param name="player">The player about to enter a mini game.</param>
    /// <returns>An empty list.</returns>
    internal static IReadOnlyList<OfflinePlayer> SnapshotPartyBots(Player player)
    {
        return [];
    }

    /// <summary>
    /// No-op because bots do not enter mini game events.
    /// </summary>
    /// <param name="leader">The party leader who entered the mini game.</param>
    /// <param name="bots">The snapshot taken before the entry.</param>
    /// <param name="definition">The definition of the entered mini game.</param>
    /// <param name="miniGame">The mini game instance the leader entered.</param>
    internal static void BringPartyBotsAlong(Player leader, IReadOnlyList<OfflinePlayer> bots, MiniGameDefinition definition, MiniGameContext miniGame)
    {
    }

    /// <summary>
    /// Determines whether a bot may enter a mini game event. Always returns <see langword="false"/>.
    /// </summary>
    /// <param name="bot">The bot in question.</param>
    /// <param name="definition">The mini game definition.</param>
    /// <param name="reason">The human-readable refusal reason.</param>
    /// <returns><see langword="false"/>.</returns>
    internal static bool IsEligible(Player bot, MiniGameDefinition definition, out string reason)
    {
        reason = "bots do not participate in events";
        return false;
    }
}
