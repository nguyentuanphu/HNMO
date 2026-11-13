// <copyright file="LogOutHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Login;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.Views.Login;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for logout packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.LogOutHandlerPlugIn_Name), Description = nameof(PlugInResources.LogOutHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("84108668-70A0-42F6-AA80-B43757F12836")]
[BelongsToGroup(LogInOutGroup.GroupKey)]
public class LogOutHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly LogoutAction _logoutAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => LogOut.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        LogOut message = packet;
        await this._logoutAction.LogoutAsync(player, (LogoutType)message.Type).ConfigureAwait(false);
    }
}