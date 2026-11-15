// <copyright file="IHnmoAccountSafetyViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// The view of HNMO's account safety (the second password, which actions need it, the locked items and what happened).
/// </summary>
public interface IHnmoAccountSafetyViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the account safety state as JSON.
    /// </summary>
    /// <param name="json">The state (see <c>AccountSafetyPlugIn</c>).</param>
    /// <param name="requested">Whether the client asked for it. Only a client which asked once gets the unasked updates
    /// (a prompt for the second password, a locked item which moved), because other clients don't know the message.</param>
    /// <returns>The task.</returns>
    ValueTask ShowAccountSafetyAsync(string json, bool requested);
}
