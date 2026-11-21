// <copyright file="WarpGateRules.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns;

using System.ComponentModel;
using HNMO.DataModel.Configuration;

/// <summary>
/// The rules of the <see cref="IWarpGateEnteringPlugIn"/>s (e.g. only Gens members into the Gens battle zone,
/// the closed Raklion boss room), for warps which don't go through a gate or the warp list (audit 12).
/// </summary>
public static class WarpGateRules
{
    /// <summary>Determines whether a warp gate rule refuses the warp. The refusing rule tells the player why.</summary>
    /// <param name="player">The player.</param>
    /// <param name="gate">The gate the player would be warped to.</param>
    /// <returns><c>true</c>, if the warp is refused.</returns>
    public static async ValueTask<bool> RefuseAsync(Player player, ExitGate gate)
    {
        if (player.GameContext.PlugInManager.GetPlugInPoint<IWarpGateEnteringPlugIn>() is not { } plugInPoint)
        {
            return false;
        }

        var eventArgs = new CancelEventArgs();
        await plugInPoint.WarpGateEnteringAsync(player, gate, eventArgs).ConfigureAwait(false);
        return eventArgs.Cancel;
    }
}
