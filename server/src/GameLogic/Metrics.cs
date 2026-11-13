// <copyright file="Metrics.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using HNMO.GameLogic.PlayerActions.Trade;
using HNMO.Pathfinding;

/// <summary>
/// Provides information about the available metrics of this project.
/// </summary>
public static class Metrics
{
    /// <summary>
    /// Gets all available meters of this project.
    /// </summary>
    public static IEnumerable<string> Meters
    {
        get
        {
            yield return GameContext.MeterName;
            yield return BaseTradeAction.MeterName;
            yield return PathFinder.MeterName;
        }
    }
}