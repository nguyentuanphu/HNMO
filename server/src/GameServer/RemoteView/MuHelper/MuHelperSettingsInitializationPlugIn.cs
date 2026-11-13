// <copyright file="MuHelperSettingsInitializationPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.MuHelper;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Offline;
using HNMO.GameLogic.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Initialized the MU Helper settings when the player enters the world.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MuHelperSettingsInitializationPlugIn_Name), Description = nameof(PlugInResources.MuHelperSettingsInitializationPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("E2306042-8484-4082-9421-7D2DC03F7D51")]
public class MuHelperSettingsInitializationPlugIn : IPlayerStateChangedPlugIn
{
    /// <inheritdoc />
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.EnteredWorld)
        {
            return default;
        }

        if (previousState == PlayerState.CharacterSelection && player is not OfflinePlayer)
        {
            // Another character may have been selected on the same connection;
            // the previous character's settings must not carry over to it.
            // Offline players and bots get their settings before entering the world.
            player.MuHelperSettings = null;
        }

        if (player.MuHelperSettings is null && player.SelectedCharacter?.MuHelperConfiguration is { } configuration)
        {
            player.MuHelperSettings = MuHelperSettingsSerializer.TryDeserialize(configuration);
        }

        return default;
    }
}
