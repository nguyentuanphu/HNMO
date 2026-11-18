// <copyright file="IHnmoVipViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>The safe own-account VIP state for a web game session.</summary>
public interface IHnmoVipViewPlugIn : IViewPlugIn
{
    /// <summary>Shows the result matching a client request identifier.</summary>
    /// <param name="requestId">The correlation identifier; zero is reserved for unsolicited refreshes.</param>
    /// <param name="json">ASCII-safe JSON from the authoritative VIP projection.</param>
    /// <returns>The send task.</returns>
    ValueTask ShowVipAsync(uint requestId, string json);
}
