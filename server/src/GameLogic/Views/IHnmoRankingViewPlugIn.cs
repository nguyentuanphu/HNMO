// <copyright file="IHnmoRankingViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// View plug-in delivering the <c>HnmoRankingState</c> (<c>F5 47</c>) snapshot to web game clients.
/// </summary>
public interface IHnmoRankingViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Sends the player ranking and add-stats status snapshot to the client.
    /// </summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether this was explicitly requested by the client.</param>
    ValueTask ShowRankingStateAsync(string json, bool requested = true);
}
