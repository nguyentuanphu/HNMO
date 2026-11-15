// <copyright file="IHnmoResetsViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// The view of HNMO's reset options (resets, relifes, today's limits, the delegated reset and what happened).
/// </summary>
public interface IHnmoResetsViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the reset state as JSON.
    /// </summary>
    /// <param name="json">The state (see <c>ResetStateWriter</c>).</param>
    /// <param name="requested">Whether the client asked for it. Only a client which asked once gets the unasked updates
    /// (a reset with the NPC or the chat command), because other clients don't know the message.</param>
    /// <returns>The task.</returns>
    ValueTask ShowResetsAsync(string json, bool requested);
}
