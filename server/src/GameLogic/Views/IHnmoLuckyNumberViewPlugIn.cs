// <copyright file="IHnmoLuckyNumberViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// View plug-in interface for sending Daily Lucky Number state (<c>F5 35</c>) to the client.
/// </summary>
public interface IHnmoLuckyNumberViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the serialized Daily Lucky Number JSON state.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether explicitly requested by the client.</param>
    ValueTask ShowLuckyNumberAsync(string json, bool requested);
}
