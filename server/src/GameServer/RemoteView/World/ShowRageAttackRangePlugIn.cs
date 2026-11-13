// <copyright file="ShowRageAttackRangePlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.World;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowRageAttackRangePlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowRageAttackRangePlugIn_Name), Description = nameof(PlugInResources.ShowRageAttackRangePlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("C480C639-A3EA-4A5C-BB82-422FCD24C920")]
[MinimumClient(6, 0, ClientLanguage.Invariant)]
public class ShowRageAttackRangePlugIn : IShowRageAttackRangePlugIn
{
    private const ushort UndefinedTargetId = 10000;

    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowRageAttackRangePlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowRageAttackRangePlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowRageAttackRangeAsync(ushort skillId, IEnumerable<IIdentifiable> targets)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        int Write()
        {
            var span = connection.Output.GetSpan(RageAttackRangeResponseRef.Length)[..RageAttackRangeResponseRef.Length];
            var packet = new RageAttackRangeResponseRef(span);
            packet.SkillId = skillId;
            var i = 0;
            foreach (var target in targets)
            {
                var block = packet[i];
                block.TargetId = target.GetId(this._player);
                i++;
            }

            for (; i < 5; i++)
            {
                var block = packet[i];
                block.TargetId = UndefinedTargetId;
            }

            return RageAttackRangeResponseRef.Length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}