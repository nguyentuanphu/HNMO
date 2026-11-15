// <copyright file="IHnmoWorldBossesViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// The view of HNMO's world bosses (the boss window and the line on the screen while a boss is alive).
/// </summary>
public interface IHnmoWorldBossesViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the state of the world bosses as JSON.
    /// </summary>
    /// <param name="json">The state (see <c>WorldBossBoardWriter</c>).</param>
    /// <param name="requested">Whether the client asked for it. Only a client which asked once gets the unasked updates
    /// (announcements, damage ranks, rewards), because other clients don't know the message.</param>
    /// <returns>The task.</returns>
    ValueTask ShowWorldBossesAsync(string json, bool requested);
}
