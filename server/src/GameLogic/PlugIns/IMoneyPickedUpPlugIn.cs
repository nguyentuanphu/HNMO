// <copyright file="IMoneyPickedUpPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// A plugin interface which is called when a player picked up money from the ground.
/// </summary>
[PlugInPoint("Money picked up", "Is called when a player picked up money from the ground.")]
[Guid("9C4D1E73-2F58-4B0A-A6E9-7D3B5C81F042")]
public interface IMoneyPickedUpPlugIn
{
    /// <summary>
    /// Is called when a player picked up money from the ground (alone or for the party).
    /// </summary>
    /// <param name="player">The player who picked it up.</param>
    /// <param name="amount">The amount of the dropped money.</param>
    /// <returns>The task.</returns>
    ValueTask MoneyPickedUpAsync(Player player, uint amount);
}
