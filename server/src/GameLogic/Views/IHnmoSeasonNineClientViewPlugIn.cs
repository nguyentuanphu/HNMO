// <copyright file="IHnmoSeasonNineClientViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Sends a Season 9 realm's client settings (e.g. whether the Season 9 minimap markers are shown) to web game clients.
/// </summary>
public interface IHnmoSeasonNineClientViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the settings.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowSeasonNineClientStateAsync(string json);
}
