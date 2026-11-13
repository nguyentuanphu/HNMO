// <copyright file="RageSkillAttackRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for rage fighter area skill attack packets (dark side).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.RageSkillAttackRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.RageSkillAttackRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A88920A7-BADA-40C0-ADF1-C1ACFE4345B3")]
[MinimumClient(6, 0, ClientLanguage.Invariant)]
internal class RageSkillAttackRequestHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly RageSkillAttackAction _attackAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => RageAttackRangeRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        RageAttackRangeRequest message = packet;
        var skillId = message.SkillId;
        var targetId = message.TargetId;
        await this._attackAction.AttackAsync(player, targetId, skillId).ConfigureAwait(false);
    }
}