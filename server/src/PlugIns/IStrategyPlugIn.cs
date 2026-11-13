// <copyright file="IStrategyPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.PlugIns;

/// <summary>
/// Interface for a strategy plugin which provides a key under which the strategy is getting registered.
/// </summary>
/// <typeparam name="TKey">The type of the key.</typeparam>
public interface IStrategyPlugIn<out TKey>
{
    /// <summary>
    /// Gets the key under which the strategy is getting registered.
    /// </summary>
    /// <value>
    /// The key.
    /// </value>
    TKey Key { get; }
}