// <copyright file="IPlayerSurrogate.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>
/// Surrogate of a player.
/// </summary>
public interface IPlayerSurrogate
{
    /// <summary>
    /// Gets the owner of this instance.
    /// </summary>
    Player Owner { get; }
}