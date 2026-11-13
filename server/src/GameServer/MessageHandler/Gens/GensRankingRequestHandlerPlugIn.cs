// <copyright file="GensRankingRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Gens;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Gens;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for the request of the gens ranking, which the client sends when it opens the gens info window.
/// The client doesn't know a specific response, so the gens info of the player is sent.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GensRankingRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.GensRankingRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2E9F6A34-C1B8-4D57-9A03-F8D4B6C1E297")]
[MinimumClient(6, 0, ClientLanguage.Invariant)]
[BelongsToGroup(GensGroupHandlerPlugIn.GroupKey)]
internal class GensRankingRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly GensActions _actions = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => GensRankingRequest.SubCode;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        await this._actions.ShowInfoAsync(player).ConfigureAwait(false);
    }
}
