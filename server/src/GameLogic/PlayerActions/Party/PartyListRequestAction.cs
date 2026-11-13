// <copyright file="PartyListRequestAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Party;

using HNMO.GameLogic;
using HNMO.GameLogic.Views.Party;

/// <summary>
/// Action to request the party list.
/// </summary>
public class PartyListRequestAction
{
    /// <summary>
    /// Requests the party list.
    /// </summary>
    /// <param name="player">The player who is requesting the list.</param>
    public async ValueTask RequestPartyListAsync(Player player)
    {
        if (player.Party is null)
        {
            return;
        }

        await player.InvokeViewPlugInAsync<IUpdatePartyListPlugIn>(p => p.UpdatePartyListAsync()).ConfigureAwait(false);
    }
}