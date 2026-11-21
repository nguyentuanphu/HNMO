// <copyright file="IHnmoEscrowViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Sends HNMO Escrow Contracts state snapshots (<c>F5 4D</c>) to the web client.
/// </summary>
public interface IHnmoEscrowViewPlugIn : IViewPlugIn
{
    /// <summary>Pushes serialized escrow contract state to the player.</summary>
    /// <param name="json">Plain-ASCII JSON state payload.</param>
    /// <param name="requested">Whether triggered by explicit client command/hotkey.</param>
    /// <returns>A task representing the send operation.</returns>
    ValueTask ShowEscrowAsync(string json, bool requested);
}
