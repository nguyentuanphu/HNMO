// <copyright file="IObjectsOutOfScopePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.World;

/// <summary>
/// Interface of a view whose implementation informs about objects which went out of the observing scope.
/// </summary>
public interface IObjectsOutOfScopePlugIn : IViewPlugIn
{
    /// <summary>
    /// Objects are out of scope.
    /// </summary>
    /// <param name="objects">The objects.</param>
    ValueTask ObjectsOutOfScopeAsync(IEnumerable<IIdentifiable> objects);
}