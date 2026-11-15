// <copyright file="IHnmoTopRacesViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// The view of HNMO's top races window (the running races, the player's rank, the tables, the prizes, the past winners).
/// </summary>
public interface IHnmoTopRacesViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the top races state as JSON.
    /// </summary>
    /// <param name="json">The state (see <c>TopRaceStateWriter</c>).</param>
    /// <param name="requested">Whether the client asked for it. Only a client which asked once gets the unasked updates
    /// (a race started or ended, a prize given), because other clients don't know the message.</param>
    /// <returns>The task.</returns>
    ValueTask ShowTopRacesAsync(string json, bool requested);
}
