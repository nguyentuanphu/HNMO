// <copyright file="CastleSiegeOwnerLogoHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.CastleSiege.Actions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handles the request for the guild logo of the castle owner.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeOwnerLogoHandlerPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeOwnerLogoHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("1B6F3EBB-D858-4785-8AE0-921659B9E748")]
[BelongsToGroup(CastleSiegeHuntZoneGroupHandlerPlugIn.GroupKey)]
internal sealed class CastleSiegeOwnerLogoHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private readonly CastleSiegeOwnerLogoAction _action = new();

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => GuildLogoOfCastleOwnerRequest.SubCode;

    /// <inheritdoc />
    public ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        return this._action.ShowOwnerLogoAsync(player, CastleSiegeHandlerContext.Get(player));
    }
}
