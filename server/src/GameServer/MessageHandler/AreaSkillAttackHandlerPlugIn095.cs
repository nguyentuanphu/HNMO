// <copyright file="AreaSkillAttackHandlerPlugIn095.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Skills;
using HNMO.GameLogic.Views.Character;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Handler for area skill attack packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AreaSkillAttackHandlerPlugIn095_Name), Description = nameof(PlugInResources.AreaSkillAttackHandlerPlugIn095_Description), ResourceType = typeof(PlugInResources))]
[Guid("2C293304-713C-4D43-9C24-6A308DD9686C")]
[MinimumClient(0, 95, ClientLanguage.Invariant)]
internal class AreaSkillAttackHandlerPlugIn095 : IPacketHandlerPlugIn
{
    private readonly AreaSkillAttackAction _attackAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public byte Key => AreaSkill095.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        AreaSkill095 message = packet;
        var skill = player.ViewPlugIns.GetPlugIn<ISkillListViewPlugIn>()?.GetSkillByIndex(message.SkillIndex);
        if (skill is null)
        {
            return;
        }

        await this._attackAction.AttackAsync(player, 0, (ushort)skill.Number, new Point(message.TargetX, message.TargetY), message.Rotation).ConfigureAwait(false);
    }
}