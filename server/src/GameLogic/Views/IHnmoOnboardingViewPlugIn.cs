// <copyright file="IHnmoOnboardingViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// The view of HNMO's new-player guide (the guide panel of the web game).
/// </summary>
public interface IHnmoOnboardingViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the state of the guide as JSON.
    /// </summary>
    /// <param name="json">The state (see <c>OnboardingStateWriter</c>).</param>
    /// <param name="requested">Whether the client asked for it. Only a client which asked once gets the unasked updates
    /// (steps done, rewards), because other clients don't know the message.</param>
    /// <returns>The task.</returns>
    ValueTask ShowOnboardingAsync(string json, bool requested);
}
