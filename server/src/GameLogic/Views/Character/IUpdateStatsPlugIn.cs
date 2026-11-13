// <copyright file="IUpdateStatsPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Character;

using HNMO.AttributeSystem;

/// <summary>
/// Interface of a view whose implementation informs about updated stats.
/// </summary>
public interface IUpdateStatsPlugIn : IViewPlugIn
{
    /// <summary>
    /// Updates the attribute value.
    /// </summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="value">The value.</param>
    ValueTask UpdateStatsAsync(AttributeDefinition attribute, float value);
}