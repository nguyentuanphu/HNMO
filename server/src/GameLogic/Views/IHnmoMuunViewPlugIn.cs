// <copyright file="IHnmoMuunViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>Sends the Season 9 Muun state to a client.</summary>
public interface IHnmoMuunViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the Muun JSON payload to the player.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether the player asked for it (a client which never asked gets nothing).</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowMuunAsync(string json, bool requested);
}
