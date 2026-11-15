// <copyright file="IHnmoMissionsViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// The view of HNMO's missions window (daily and weekly missions, streak, login calendar, earned coins).
/// </summary>
public interface IHnmoMissionsViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the state of the missions as JSON.
    /// </summary>
    /// <param name="json">The state (see <c>MissionBoardWriter</c>).</param>
    /// <param name="requested">Whether the client asked for it. Only a client which asked once gets the unasked updates
    /// (e.g. a finished mission), because other clients don't know the message.</param>
    /// <returns>The task.</returns>
    ValueTask ShowMissionsAsync(string json, bool requested);
}
