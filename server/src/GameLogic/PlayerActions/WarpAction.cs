// <copyright file="WarpAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;

/// <summary>
/// Action to warp to another place.
/// </summary>
public class WarpAction
{
    /// <summary>
    /// The multiplier of the warp costs for outlaws from PK level 2 up (Season 6/9 server suites).
    /// </summary>
    private const int PlayerKillerCostsMultiplier = 50;

    /// <summary>
    /// The multiplier of the warp costs for outlaws from PK level 2 up into a battle zone of the gens (owner, 26 Nov 2026).
    /// </summary>
    private const int BattleZoneOutlawCostsMultiplier = 2;

    /// <summary>
    /// Warps the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="warpInfo">The warp information.</param>
    public async ValueTask WarpToAsync(Player player, WarpInfo warpInfo)
    {
        if (await PlugIns.Arena.ArenaRules.RefuseAsync(player, PlugIns.Arena.ArenaRules.Action.Warp).ConfigureAwait(false))
        {
            return;
        }

        // Not while dead, duelling or trading: a trade state can't change to the map change state, so the
        // player would be taken off its map without a map change (and pay the zen).
        if (warpInfo.Gate?.Map is { } targetMap && HnmoWarpGuard.Refusal(player, targetMap) is { } refusal)
        {
            await player.ShowBlueMessageAsync(HnmoWarpGuard.TextFor(player, refusal)).ConfigureAwait(false);
            return;
        }

        if (warpInfo.Gate is { } gate
            && player.GameContext.PlugInManager.GetPlugInPoint<IWarpGateEnteringPlugIn>() is { } plugInPoint)
        {
            // Checked before the requirements, because the costs are paid when they're fulfilled.
            var eventArgs = new CancelEventArgs();
            await plugInPoint.WarpGateEnteringAsync(player, gate, eventArgs).ConfigureAwait(false);
            if (eventArgs.Cancel)
            {
                return;
            }
        }

        if (this.CheckRequirements(player, warpInfo, out var errorMessage))
        {
            await player.WarpToAsync(warpInfo.Gate!).ConfigureAwait(false);
        }
        else
        {
            await player.ShowBlueMessageAsync(errorMessage).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets the warp costs. As in the Season 6/9 server suites, outlaws from PK level 2 up pay
    /// <see cref="PlayerKillerCostsMultiplier"/> times the costs.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="warpInfo">The warp information.</param>
    /// <returns>The costs in zen.</returns>
    internal static int GetCosts(Player player, WarpInfo warpInfo)
    {
        if (player.SelectedCharacter?.State < HeroState.PlayerKiller1stStage || player.SelectedCharacter is null)
        {
            return warpInfo.Costs;
        }

        // Into a battle zone of the gens an outlaw pays twice the costs instead (owner, 26 Nov 2026, audit Q8); leaving
        // it, or any other map, keeps the 50 times. Without the gens system there is no battle zone.
        return warpInfo.Gate?.Map is { } map
               && HNMO.GameLogic.Gens.GensFeaturePlugIn.GetConfiguration(player.GameContext) is { } gens
               && gens.IsBattleZone(map.Number)
            ? warpInfo.Costs * BattleZoneOutlawCostsMultiplier
            : warpInfo.Costs * PlayerKillerCostsMultiplier;
    }

    private bool CheckRequirements(Player player, WarpInfo warpInfo, [MaybeNullWhen(true)] out string errorMessage)
    {
        errorMessage = null;

        var requirement = player.SelectedCharacter?.GetEffectiveMoveLevelRequirement(warpInfo.LevelRequirement);
        if (requirement > player.Attributes?[Stats.Level])
        {
            errorMessage = $"You need to be level {requirement} in order to warp";
            return false;
        }

        if (warpInfo.Gate?.Map is null)
        {
            errorMessage = "The warp target is not initialized";
            return false;
        }

        if (warpInfo.Gate.Map.TryGetRequirementError(player, out var message))
        {
            errorMessage = message;
            return false;
        }

        // Money check should be last to avoid getting zen when other checks failed
        var costs = GetCosts(player, warpInfo);
        if (!player.TryRemoveMoney(costs))
        {
            errorMessage = $"You need {costs} in order to warp";
            return false;
        }

        return true;
    }
}