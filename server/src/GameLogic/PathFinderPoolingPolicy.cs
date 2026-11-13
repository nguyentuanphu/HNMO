// <copyright file="PathFinderPoolingPolicy.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using Microsoft.Extensions.ObjectPool;
using HNMO.Pathfinding;

/// <summary>
/// The <see cref="PooledObjectPolicy{PathFinder}"/> which implements the creation
/// of the <see cref="PathFinder"/> with an <see cref="ScopedGridNetwork"/>.
/// </summary>
public class PathFinderPoolingPolicy : PooledObjectPolicy<PathFinder>
{
    /// <inheritdoc />
    public override PathFinder Create()
    {
        return new PathFinder(new ScopedGridNetwork());
    }

    /// <inheritdoc />
    public override bool Return(PathFinder obj)
    {
        return true;
    }
}