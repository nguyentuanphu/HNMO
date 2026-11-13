// <copyright file="ItemRegistrationNpcTalkPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Items.ItemRegistration;

using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views.NPC;
using HNMO.PlugIns;

/// <summary>
/// Plugin to handle dialogue with any item registration NPC.
/// </summary>
[Guid("F0C60C89-6F12-40DB-A24F-418E6DE04300")]
[PlugIn]
public class ItemRegistrationNpcTalkPlugIn : IPlayerTalkToNpcPlugIn
{
    /// <inheritdoc />
    public async ValueTask PlayerTalksToNpcAsync(Player player, NonPlayerCharacter npc, NpcTalkEventArgs eventArgs)
    {
        var feature = player.GameContext.FeaturePlugIns.GetPlugIn<GameLogic.PlugIns.ItemRegistration.ItemRegistrationFeaturePlugIn>();
        if (feature?.Configuration is not { } config)
        {
            return;
        }

        var rule = config.Rules.FirstOrDefault(r => r.NpcNumber == npc.Definition.Number);

        if (rule == null)
        {
            return;
        }

        var strategy = player.GameContext.PlugInManager.GetStrategy<short, IItemRegistrationStrategy>(npc.Definition.Number);
        if (strategy == null)
        {
            return;
        }

        eventArgs.HasBeenHandled = true;
        eventArgs.LeavesDialogOpen = true;

        await strategy.OpenDialogAsync(player).ConfigureAwait(false);
    }
}
