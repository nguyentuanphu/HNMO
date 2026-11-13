// <copyright file="IPartyHealthViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Party;

/// <summary>
/// The view plugin for party information.
/// </summary>
public interface IPartyHealthViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Updates the party health.
    /// </summary>
    ValueTask UpdatePartyHealthAsync();

    /// <summary>
    /// Determines whether a health update is needed.
    /// </summary>
    /// <returns>If set to <c>true</c>, a health update is needed; Otherwise not.</returns>
    bool IsHealthUpdateNeeded();
}