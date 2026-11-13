// <copyright file="IChangeTerrainAttributesViewPlugin.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.MiniGames;

using HNMO.GameLogic.Views;

/// <summary>
/// Interface for view plugins which update the terrain attributes.
/// </summary>
public interface IChangeTerrainAttributesViewPlugin : IViewPlugIn
{
    /// <summary>
    /// Updates the terrain attributes.
    /// </summary>
    /// <param name="attribute">The type of terrain attribute.</param>
    /// <param name="setAttribute">Specifies, if the attribute should be set (true), or removed (false).</param>
    /// <param name="areas">The areas of terrain.</param>
    ValueTask ChangeAttributesAsync(TerrainAttributeType attribute, bool setAttribute, IReadOnlyCollection<(byte StartX, byte StartY, byte EndX, byte EndY)> areas);
}