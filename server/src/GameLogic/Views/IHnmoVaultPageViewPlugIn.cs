// <copyright file="IHnmoVaultPageViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>Shows the vault pages (the page in use, the pages unlocked and the price of one more) to a player.</summary>
public interface IHnmoVaultPageViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the vault page state.</summary>
    /// <param name="json">Plain-ASCII JSON state.</param>
    /// <returns>A task representing the send operation.</returns>
    ValueTask ShowVaultPagesAsync(string json);
}
