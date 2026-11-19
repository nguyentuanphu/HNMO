// <copyright file="IHnmoMarketViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// View plug-in interface for sending Item Market state (<c>F5 39</c>) to the client.
/// </summary>
public interface IHnmoMarketViewPlugIn : IViewPlugIn
{
    /// <summary>Sends serialized market JSON to the client.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether explicitly requested by the client.</param>
    ValueTask ShowMarketAsync(string json, bool requested);
}
