// <copyright file="IShowMiniGameOpeningStatePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.MiniGames;

using HNMO.GameLogic.Views;

/// <summary>
/// Interface for view plugins which shows the opening state of a mini game event.
/// </summary>
public interface IShowMiniGameOpeningStatePlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the opening state of the mini game event.
    /// </summary>
    /// <param name="miniGameType">Type of the mini game.</param>
    /// <param name="timeUntilOpening">The time until the mini game opens.</param>
    /// <param name="playerCount">The player count, if the game is already open.</param>
    ValueTask ShowOpeningStateAsync(MiniGameType miniGameType, TimeSpan? timeUntilOpening, int playerCount);
}