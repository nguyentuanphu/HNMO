// <copyright file="IUpdateMiniGameStateViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.MiniGames;

using HNMO.GameLogic.Views;

/// <summary>
/// Interface for view plugins which show the state of an mini game event.
/// </summary>
public interface IUpdateMiniGameStateViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Updates the state of the mini game.
    /// </summary>
    /// <param name="type">The type of the mini game.</param>
    /// <param name="state">The new state of the game.</param>
    ValueTask UpdateStateAsync(MiniGameType type, MiniGameState state);
}