// <copyright file="IHnmoCustomRanksViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// View plug-in delivering custom rank / title definitions and active player titles to web clients.
/// </summary>
public interface IHnmoCustomRanksViewPlugIn : IViewPlugIn
{
    /// <summary>Sends custom rank definitions and active player titles as JSON.</summary>
    /// <param name="json">The serialized state JSON.</param>
    /// <param name="requested">Whether triggered by a client packet.</param>
    /// <returns>The task.</returns>
    ValueTask ShowCustomRanksStateAsync(string json, bool requested = true);
}
