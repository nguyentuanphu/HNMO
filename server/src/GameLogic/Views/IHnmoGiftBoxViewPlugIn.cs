// <copyright file="IHnmoGiftBoxViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>The view of HNMO's gift box / mailbox window.</summary>
public interface IHnmoGiftBoxViewPlugIn : IViewPlugIn
{
    /// <summary>Sends correlated mailbox JSON state back to the requesting web player.</summary>
    /// <param name="requestId">Correlation identifier echoed from <c>HnmoGiftBoxRequest</c>.</param>
    /// <param name="json">Safe projected mailbox JSON.</param>
    /// <returns>The task.</returns>
    ValueTask ShowGiftBoxAsync(uint requestId, string json);
}
