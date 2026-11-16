// <copyright file="IHnmoSeasonPlanViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Sends HNMO Season Plan timeline state to web game clients.
/// </summary>
public interface IHnmoSeasonPlanViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the Season Plan JSON payload.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether explicitly requested by the client.</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowSeasonPlanAsync(string json, bool requested);
}
