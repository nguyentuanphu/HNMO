// <copyright file="CastleSiegeHuntZoneEnterHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.CastleSiege.Actions;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handles Land of Trials entry requests.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeHuntZoneEnterHandlerPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeHuntZoneEnterHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("9D8A4C2C-44E2-4FA3-9991-DA9FA8DED2AD")]
[BelongsToGroup(CastleSiegeHuntZoneGroupHandlerPlugIn.GroupKey)]
internal sealed class CastleSiegeHuntZoneEnterHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly CastleSiegeHuntZoneEnterAction _action = new();

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => CastleSiegeHuntingZoneEnterRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < CastleSiegeHuntingZoneEnterRequest.Length)
        {
            return;
        }

        var success = await this._action.EnterAsync(player, CastleSiegeHandlerContext.Get(player)).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<ICastleSiegeHuntZoneResultPlugIn>(
                view => view.ShowEnterResultAsync(success))
            .ConfigureAwait(false);
    }
}
