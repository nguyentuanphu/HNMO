// <copyright file="IShowMoneyDropPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.World;

using HNMO.Pathfinding;

/// <summary>
/// Interface of a view whose implementation informs about dropped zen.
/// </summary>
public interface IShowMoneyDropPlugIn : IViewPlugIn
{
    /// <summary>
    /// Show money on the ground.
    /// </summary>
    /// <param name="itemId">The id of the drop in the map.</param>
    /// <param name="isFreshDrop">If set to <c>true</c>, it's a fresh drop.</param>
    /// <param name="amount">The amount of money which was dropped.</param>
    /// <param name="point">The position of the money on the map.</param>
    ValueTask ShowMoneyAsync(ushort itemId, bool isFreshDrop, uint amount, Point point);
}