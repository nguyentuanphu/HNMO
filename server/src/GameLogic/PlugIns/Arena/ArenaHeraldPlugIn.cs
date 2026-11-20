// <copyright file="ArenaHeraldPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Properties;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// The HNMO Arena Herald (owner's choice, 2026-11-20): talking to it opens the Arena window, as Alt+A does. The web game
/// gets <c>{"open":true}</c> in the Arena state packet (F5 53) and asks for the full state itself.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ArenaHeraldPlugIn_Name), Description = nameof(PlugInResources.ArenaHeraldPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D81F4C36-5A92-4E07-B3C8-1F6E2A9D7B54")]
public sealed class ArenaHeraldPlugIn : IPlayerTalkToNpcPlugIn
{
    /// <summary>The Herald's NPC number.</summary>
    public const short HeraldNumber = 595;

    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        if (npc.Definition.Number == HeraldNumber)
        {
            eventArgs.HasBeenHandled = true;
            await player.InvokeViewPlugInAsync<IHnmoArenaTravelViewPlugIn>(p => p.ShowArenaTravelAsync("{\"open\":true}")).ConfigureAwait(false);
        }
    }
}
