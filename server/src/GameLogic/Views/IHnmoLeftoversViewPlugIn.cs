// <copyright file="IHnmoLeftoversViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Sends Combining Leftovers &amp; Surplus Material Exchange state to web game clients.
/// </summary>
public interface IHnmoLeftoversViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the leftovers state JSON payload.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether explicitly requested or acted upon by the client.</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowLeftoversAsync(string json, bool requested);
}
