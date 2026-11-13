// <copyright file="RageSkillAttackHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handler for rage fighter area skill attack packets (beast uppercut, chain drive, dragon slasher, dark side).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.RageSkillAttackHandlerPlugIn_Name), Description = nameof(PlugInResources.RageSkillAttackHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("D09ED5FB-DB6F-4707-A740-144CF2BA5D92")]
[MinimumClient(6, 0, ClientLanguage.Invariant)]
internal class RageSkillAttackHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public byte Key => RageAttackRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        RageAttackRequest message = packet;
        var skillId = message.SkillId;
        var targetId = message.TargetId;

        var target = player.GetObject(targetId) as IAttackable;
        if (player.SkillList is null || player.SkillList.GetSkill(message.SkillId) is not { } skill
                                     || target is null || !target.IsInRange(player.Position, skill.Skill!.Range))
        {
            return;
        }

        await player.ForEachWorldObserverAsync<IShowRageAttackPlugIn>(p => p.ShowAttackAsync(player, target, skillId), true).ConfigureAwait(false);
    }
}