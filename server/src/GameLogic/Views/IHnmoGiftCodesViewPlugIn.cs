// <copyright file="IHnmoGiftCodesViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// The view of HNMO's gift code window (the result of an entered code, the rewards waiting for room in the bag, the
/// account's last codes).
/// </summary>
public interface IHnmoGiftCodesViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the gift code state as JSON.
    /// </summary>
    /// <param name="json">The state (see <c>GiftCodeStateWriter</c>).</param>
    /// <param name="requested">Whether the client asked for it. Only a client which asked once gets the unasked updates
    /// (a code entered with the chat command, waiting rewards given), because other clients don't know the message.</param>
    /// <returns>The task.</returns>
    ValueTask ShowGiftCodesAsync(string json, bool requested);
}
