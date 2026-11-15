// <copyright file="IItemCraftedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.NPC;
using HNMO.PlugIns;

/// <summary>
/// A plugin interface which is called when a player mixed items at a crafting NPC, e.g. the chaos machine.
/// </summary>
[PlugInPoint("Items mixed", "Is called when a player mixed items at a crafting NPC, e.g. the chaos machine.")]
[Guid("5E2B7C41-8A93-4D6F-B1E0-3C7A9F24D815")]
public interface IItemCraftedPlugIn
{
    /// <summary>
    /// Is called when a player mixed items.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="result">The result of the mix.</param>
    /// <returns>The task.</returns>
    ValueTask ItemCraftedAsync(Player player, CraftingResult result);
}
