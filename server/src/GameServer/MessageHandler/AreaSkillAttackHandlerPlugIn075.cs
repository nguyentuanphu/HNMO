// <copyright file="AreaSkillAttackHandlerPlugIn075.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.GameLogic.Views.Character;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Handler for area skill attack packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AreaSkillAttackHandlerPlugIn075_Name), Description = nameof(PlugInResources.AreaSkillAttackHandlerPlugIn075_Description), ResourceType = typeof(PlugInResources))]
[Guid("A03821B8-2A8E-4A1D-A45D-C92C7621DC8A")]
internal class AreaSkillAttackHandlerPlugIn075 : IPacketHandlerPlugIn
{
    private readonly AreaSkillAttackAction _attackAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AreaSkill075.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        AreaSkill075 message = packet;
        var skill = player.ViewPlugIns.GetPlugIn<ISkillListViewPlugIn>()?.GetSkillByIndex(message.SkillIndex);
        if (skill is null)
        {
            return;
        }

        await this._attackAction.AttackAsync(player, 0, (ushort)skill.Number, new Point(message.TargetX, message.TargetY), message.Rotation).ConfigureAwait(false);
    }
}