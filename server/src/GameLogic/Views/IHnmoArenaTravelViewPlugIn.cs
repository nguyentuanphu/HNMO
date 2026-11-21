// <copyright file="IHnmoArenaTravelViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>Shows the HNMO Arena's travel state (and a travel ticket) to a player.</summary>
public interface IHnmoArenaTravelViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the Arena travel state.</summary>
    /// <param name="json">Plain-ASCII JSON state.</param>
    /// <returns>A task representing the send operation.</returns>
    ValueTask ShowArenaTravelAsync(string json);
}
