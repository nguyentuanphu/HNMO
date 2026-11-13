// <copyright file="CastleSiegeStatueListHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.CastleSiege.NPC;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handles requests for the Castle Siege Guardian Statue list.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeStatueListHandlerPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeStatueListHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("BC11A3E4-A72F-4484-8388-5A252B448B62")]
[BelongsToGroup(CastleSiegeNpcGroupHandlerPlugIn.GroupKey)]
internal sealed class CastleSiegeStatueListHandlerPlugIn : CastleSiegeNpcListHandlerBase
{
    /// <inheritdoc />
    public override byte Key => CastleSiegeStatueListRequest.SubCode;

    /// <inheritdoc />
    protected override short MonsterNumber => CastleSiegeStatue.MonsterNumber;
}
