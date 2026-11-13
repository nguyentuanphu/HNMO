// <copyright file="CastleSiegeGateListHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.CastleSiege.NPC;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handles requests for the Castle Siege gate list.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeGateListHandlerPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeGateListHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("904BE911-B46C-4C7A-BBC2-C5B845592C65")]
[BelongsToGroup(CastleSiegeNpcGroupHandlerPlugIn.GroupKey)]
internal sealed class CastleSiegeGateListHandlerPlugIn : CastleSiegeNpcListHandlerBase
{
    /// <inheritdoc />
    public override byte Key => CastleSiegeGateListRequest.SubCode;

    /// <inheritdoc />
    protected override short MonsterNumber => CastleSiegeGate.MonsterNumber;
}
