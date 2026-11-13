// <copyright file="IFruitConsumptionResponsePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Character;

using HNMO.AttributeSystem;

/// <summary>
/// Interface of a view whose implementation informs about the result of a fruit consumption request.
/// </summary>
public interface IFruitConsumptionResponsePlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the response.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="statPoints">The stat points.</param>
    /// <param name="statAttribute">The stat attribute.</param>
    ValueTask ShowResponseAsync(FruitConsumptionResult result, ushort statPoints, AttributeDefinition statAttribute);
}