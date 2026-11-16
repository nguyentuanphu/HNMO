// <copyright file="IHnmoCatchupCapViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Sends Catch-up Level/Reset Cap state to web game clients.
/// </summary>
public interface IHnmoCatchupCapViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the Catch-up Cap JSON payload.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether explicitly requested by the client.</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowCatchupCapAsync(string json, bool requested);
}
