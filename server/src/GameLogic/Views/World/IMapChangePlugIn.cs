// <copyright file="IMapChangePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation informs about a map change of the current player.
/// </summary>
public interface IMapChangePlugIn : IViewPlugIn
{
    /// <summary>
    /// Will be called then the map got changed. The new map and coordinates are defined in the player.SelectedCharacter.CurrentMap.
    /// </summary>
    ValueTask MapChangeAsync();

    /// <summary>
    /// Is called when a map change request failed.
    /// </summary>
    ValueTask MapChangeFailedAsync();
}