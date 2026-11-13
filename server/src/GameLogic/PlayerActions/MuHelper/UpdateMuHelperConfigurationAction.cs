// <copyright file="UpdateMuHelperConfigurationAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.MuHelper;

using HNMO.GameLogic.Views.MuHelper;

/// <summary>
/// Action to update the mu helper configuration.
/// </summary>
public class UpdateMuHelperConfigurationAction
{
    /// <summary>
    /// Toggle mu bot status.
    /// </summary>
    /// <param name="player">the player.</param>
    /// <param name="data">mu bot data to be saved.</param>
    public async ValueTask SaveDataAsync(Player player, Memory<byte> data)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        try
        {
            character.MuHelperConfiguration = data.ToArray();
            await player.InvokeViewPlugInAsync<IMuHelperConfigurationUpdatePlugIn>(p => p.UpdateMuHelperConfigurationAsync(character.MuHelperConfiguration)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            player.Logger.LogWarning($"Cannot save MU Helper Configuration => {e}");
        }
    }
}