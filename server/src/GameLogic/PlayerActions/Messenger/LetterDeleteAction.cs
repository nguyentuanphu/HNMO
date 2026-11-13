// <copyright file="LetterDeleteAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Messenger;

using HNMO.GameLogic.Views.Messenger;
using HNMO.Interfaces;

/// <summary>
/// Action to delete a letter.
/// </summary>
public class LetterDeleteAction
{
    /// <summary>
    /// Deletes the letter.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="letter">The letter.</param>
    public async ValueTask DeleteLetterAsync(Player player, LetterHeader letter)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());
        if (player.SelectedCharacter is not { } character)
        {
            player.Logger.LogWarning("no character selected, player {0}", player);
            return;
        }

        var letterIndex = character.Letters.IndexOf(letter);
        character.Letters.RemoveAt(letterIndex);
        await player.InvokeViewPlugInAsync<ILetterDeletedPlugIn>(p => p.LetterDeletedAsync((ushort)letterIndex)).ConfigureAwait(false);
    }
}