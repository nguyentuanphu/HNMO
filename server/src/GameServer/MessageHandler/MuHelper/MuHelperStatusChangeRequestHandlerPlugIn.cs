// <copyright file="MuHelperStatusChangeRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.MuHelper;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.MuHelper;
using HNMO.GameLogic.PlayerActions.MuHelper;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for MU Helper use requests.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MuHelperStatusChangeRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.MuHelperStatusChangeRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("91B5040E-44B6-41FC-A0AB-A881770B2A16")]
[BelongsToGroup(MuHelperGroupHandler.GroupKey)]
public class MuHelperStatusChangeRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly ChangeMuHelperStateAction _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => MuHelperStatusChangeRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        MuHelperStatusChangeRequest message = packet;
        var status = message.PauseStatus ? MuHelperStatus.Disabled : MuHelperStatus.Enabled;
        await this._action.ChangeHelperStateAsync(player, status).ConfigureAwait(false);
    }
}