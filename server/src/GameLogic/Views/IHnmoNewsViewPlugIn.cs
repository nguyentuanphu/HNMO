// <copyright file="IHnmoNewsViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Interface of a view plug-in that sends new news post notices (<c>C2 F5 2A</c>) to web game clients.
/// </summary>
public interface IHnmoNewsViewPlugIn : IViewPlugIn
{
    /// <summary>
    /// Sends the latest published news notice JSON to the player's client.
    /// </summary>
    /// <param name="json">The serialized notice payload.</param>
    ValueTask ShowNewsNoticeAsync(string json);
}
