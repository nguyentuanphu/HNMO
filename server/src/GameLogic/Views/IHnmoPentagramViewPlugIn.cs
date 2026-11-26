// <copyright file="IHnmoPentagramViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Sends the Season 9 pentagram state and the elemental part of hits to the web game.
/// </summary>
public interface IHnmoPentagramViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the pentagram JSON payload to the player.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowPentagramAsync(string json);

    /// <summary>Shows the elemental damage of a hit the player dealt.</summary>
    /// <param name="targetId">The id of the target.</param>
    /// <param name="damage">The elemental damage.</param>
    /// <param name="element">The player's element.</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowElementalHitAsync(ushort targetId, int damage, byte element);
}
