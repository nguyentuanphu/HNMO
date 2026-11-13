// <copyright file="IWorldObserver.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// Interface of an world observer.
/// </summary>
public interface IWorldObserver : ILoggerOwner
{
    /// <summary>
    /// Gets the view plug ins.
    /// </summary>
    /// <value>
    /// The view plug ins.
    /// </value>
    ICustomPlugInContainer<IViewPlugIn> ViewPlugIns { get; }
}