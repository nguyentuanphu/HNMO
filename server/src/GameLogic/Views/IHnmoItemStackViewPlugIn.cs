// <copyright file="IHnmoItemStackViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>Shows HNMO's item stack kinds (which items stack, how many, the unlocks) to a player.</summary>
public interface IHnmoItemStackViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the stack state.</summary>
    /// <param name="json">Plain-ASCII JSON state.</param>
    /// <returns>A task representing the send operation.</returns>
    ValueTask ShowItemStacksAsync(string json);
}
