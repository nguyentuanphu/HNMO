// <copyright file="IShowCreatedCharacterPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.Views.Character;

/// <summary>
/// Interface of a view whose implementation informs about the successfully created character.
/// </summary>
public interface IShowCreatedCharacterPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the created character.
    /// </summary>
    /// <param name="character">The character.</param>
    ValueTask ShowCreatedCharacterAsync(DataModel.Entities.Character character);
}