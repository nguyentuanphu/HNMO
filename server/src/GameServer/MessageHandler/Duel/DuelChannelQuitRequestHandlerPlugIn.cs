// <copyright file="DuelChannelQuitRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Duel;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Duel;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for duel channel quit request packets (new duel system), which is sent by a spectator.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.DuelChannelQuitRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.DuelChannelQuitRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("47E7FBBD-F5FE-41D1-8086-934D4DE86828")]
[MinimumClient(4, 0, ClientLanguage.Invariant)]
[BelongsToGroup(DuelGroupHandlerPlugIn.GroupKey)]
internal class DuelChannelQuitRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly DuelActions _action = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => DuelChannelQuitRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._action.HandleDuelChannelQuitRequestAsync(player).ConfigureAwait(false);
    }
}