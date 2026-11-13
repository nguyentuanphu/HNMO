// <copyright file="LogOutByCheatDetectionHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Login;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.Views.Login;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for logout by cheat packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.LogOutByCheatDetectionHandlerPlugIn_Name), Description = nameof(PlugInResources.LogOutByCheatDetectionHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("AE611B1E-3E3D-4189-B39C-79696D27BFBD")]
[BelongsToGroup(LogInOutGroup.GroupKey)]
public class LogOutByCheatDetectionHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly LogoutAction _logoutAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public byte Key => LogOutByCheatDetection.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        player.Logger.LogError("Logged out by detected cheat on client side. Player: {player}", player);
        await this._logoutAction.LogoutAsync(player, LogoutType.CloseGame).ConfigureAwait(false);
    }
}