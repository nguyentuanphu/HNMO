// <copyright file="PingResponseHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameServer.Properties;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for the <see cref="PingResponse"/> (<c>0x71</c>) keep-alive response sent by the client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PingResponseHandlerPlugIn_Name), Description = nameof(PlugInResources.PingResponseHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("27A54C91-5F47-4B83-8C40-29B9D4715E01")]
internal class PingResponseHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => PingResponse.Code;

    /// <inheritdoc />
    public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        player.ReportAlive();
        return ValueTask.CompletedTask;
    }
}
