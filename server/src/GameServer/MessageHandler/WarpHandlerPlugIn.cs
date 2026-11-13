// <copyright file="WarpHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.Properties;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;
using PlugInResources = HNMO.GameServer.Properties.PlugInResources;

/// <summary>
/// Handler for warp request packets.
/// This one is called when a player uses the warp list.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.WarpHandlerPlugIn_Name), Description = nameof(PlugInResources.WarpHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3d261a26-4357-4367-b999-703ea936f4e9")]
internal class WarpHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly WarpAction _warpAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => WarpCommandRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        WarpCommandRequest request = packet;
        ushort warpInfoIndex = request.WarpInfoIndex;
        var warpInfo = player.GameContext.Configuration.WarpList?.FirstOrDefault(info => info.Index == warpInfoIndex);
        if (warpInfo != null)
        {
            await this._warpAction.WarpToAsync(player, warpInfo).ConfigureAwait(false);
        }
        else
        {
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.UnknownWarpIndex), warpInfoIndex).ConfigureAwait(false);
        }
    }
}