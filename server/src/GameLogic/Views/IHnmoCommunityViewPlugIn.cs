// <copyright file="IHnmoCommunityViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// The view of HNMO's community window (the Discord and Zalo coins, the community events, the waiting rewards).
/// </summary>
public interface IHnmoCommunityViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the community state as JSON.
    /// </summary>
    /// <param name="json">The state (see <c>CommunityStateWriter</c>).</param>
    /// <param name="requested">Whether the client asked for it. Only a client which asked once gets the unasked updates
    /// (coins paid, rewards given), because other clients don't know the message.</param>
    /// <returns>The task.</returns>
    ValueTask ShowCommunityAsync(string json, bool requested);
}
