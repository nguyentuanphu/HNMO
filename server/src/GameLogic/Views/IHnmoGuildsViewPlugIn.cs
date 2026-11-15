// <copyright file="IHnmoGuildsViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// The view of HNMO's guild levels (the guild window's level, experience, ladder rank and buff).
/// </summary>
public interface IHnmoGuildsViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the guild levels state as JSON.
    /// </summary>
    /// <param name="json">The state (see <c>GuildStateWriter</c>).</param>
    /// <param name="requested">Whether the client asked for it. Only a client which asked once gets the unasked updates
    /// (level-ups, ladder rewards), because other clients don't know the message.</param>
    /// <returns>The task.</returns>
    ValueTask ShowGuildsAsync(string json, bool requested);
}
