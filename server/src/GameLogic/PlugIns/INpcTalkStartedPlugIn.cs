// <copyright file="INpcTalkStartedPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using HNMO.GameLogic.NPC;
using HNMO.PlugIns;

/// <summary>
/// A plugin interface which is called when a player starts talking to any NPC (a merchant, the vault, a quest NPC, ...),
/// before its window or dialog opens. Unlike <see cref="IPlayerTalkToNpcPlugIn"/>, it can't handle the talk.
/// </summary>
[Guid("3F7C2A90-6B1E-4D58-9A24-E81C05B7D3F6")]
[PlugInPoint("NPC talk started", "Plugins which are called when a player starts talking to any NPC.")]
public interface INpcTalkStartedPlugIn
{
    /// <summary>
    /// Is called when a player starts talking to an NPC.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The NPC.</param>
    /// <returns>The task.</returns>
    ValueTask NpcTalkStartedAsync(Player player, NonPlayerCharacter npc);
}
