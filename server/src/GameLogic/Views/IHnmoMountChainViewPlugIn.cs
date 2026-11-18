// <copyright file="IHnmoMountChainViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Sends HNMO Mount Chain collection and upgrade state to web game clients.
/// </summary>
public interface IHnmoMountChainViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the mount chain JSON payload.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether explicitly requested by the client.</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowMountChainAsync(string json, bool requested);
}
