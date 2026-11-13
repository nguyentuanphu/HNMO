// <copyright file="MoveType.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.World;

/// <summary>
/// Specifies the move type of objects.
/// </summary>
public enum MoveType
{
    /// <summary>
    /// Moving by walking.
    /// </summary>
    Walk,

    /// <summary>
    /// Moving by instantly moving the object on the map, without animations.
    /// </summary>
    Instant,

    /// <summary>
    /// Moving by teleporting the object on the map.
    /// </summary>
    Teleport,
}