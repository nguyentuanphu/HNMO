// <copyright file="IShowAreaSkillAnimationPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.World;

using HNMO.Pathfinding;

/// <summary>
/// Interface of a view whose implementation informs about area skill animations of objects.
/// </summary>
public interface IShowAreaSkillAnimationPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the area skill animation.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="skill">The skill.</param>
    /// <param name="point">The coordinates.</param>
    /// <param name="rotation">The rotation.</param>
    ValueTask ShowAreaSkillAnimationAsync(Player player, Skill skill, Point point, byte rotation);
}