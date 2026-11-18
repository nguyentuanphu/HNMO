// <copyright file="IHnmoMarriageViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// View plug-in interface for sending HNMO Marriage state (<c>F5 37</c>) to the client.
/// </summary>
public interface IHnmoMarriageViewPlugIn : IViewPlugIn
{
    /// <summary>Sends serialized Marriage JSON to the client.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether requested explicitly by the client.</param>
    ValueTask ShowMarriageAsync(string json, bool requested);
}
