// <copyright file="EnterMarketPlaceHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameServer.MessageHandler.MuHelper;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for the <see cref="EnterMarketPlaceRequest"/>, sent by the 'Warp' button of the
/// Market Union Member Julia window. It delegates to the <see cref="EnterMarketPlaceAction"/>,
/// which warps the player between Lorencia and the Loren Market.
/// </summary>
[PlugIn]
[Display(Name = nameof(Properties.PlugInResources.EnterMarketPlace_Name), Description = nameof(Properties.PlugInResources.EnterMarketPlace_Description), ResourceType = typeof(HNMO.GameServer.Properties.PlugInResources))]
[Guid("5e028dcf-a5af-40a0-b958-2deb38aae4bc")]
[BelongsToGroup(MuHelperGroupHandler.GroupKey)]
internal class EnterMarketPlaceHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private const string PlugInName = "Enter Market Place Handler";

    private const string PlugInDescription = "Handler which warps a player between Lorencia and the Loren Market when using the 'Warp' button of Market Union Member Julia.";

    private readonly EnterMarketPlaceAction _enterMarketPlaceAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => EnterMarketPlaceRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < EnterMarketPlaceRequest.Length)
        {
            return;
        }

        await this._enterMarketPlaceAction.WarpToMarketPlaceAsync(player).ConfigureAwait(false);
    }
}
