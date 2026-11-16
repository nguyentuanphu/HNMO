// <copyright file="IHnmoJewelBankViewPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views;

/// <summary>
/// Sends HNMO's Jewel Bank balances, bag jewels, and master skill points to the web game.
/// </summary>
public interface IHnmoJewelBankViewPlugIn : IViewPlugIn
{
    /// <summary>Sends the Jewel Bank JSON payload to the player.</summary>
    /// <param name="json">Plain-ASCII JSON payload.</param>
    /// <param name="requested">Whether explicitly requested by the user.</param>
    /// <returns>A task representing the send.</returns>
    ValueTask ShowJewelBankAsync(string json, bool requested);
}
