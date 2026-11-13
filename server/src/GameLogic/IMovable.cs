// <copyright file="IMovable.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using HNMO.Pathfinding;

/// <summary>
///  Interface for an object which supports to be moved on a map.
/// </summary>
public interface IMovable
{
    /// <summary>
    /// Moves the object to the specified target coordinates.
    /// </summary>
    /// <param name="target">The target coordinates.</param>
    ValueTask MoveAsync(Point target);
}