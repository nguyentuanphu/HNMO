// <copyright file="IHnmoTopUpMilestonesViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// View plug-in sending HNMO top-up milestone state to the web client.
/// </summary>
public interface IHnmoTopUpMilestonesViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the top-up milestones JSON state.
    /// </summary>
    /// <param name="json">The plain-ASCII JSON state.</param>
    /// <param name="requested">Whether the client requested it explicitly.</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowTopUpMilestonesAsync(string json, bool requested);
}
