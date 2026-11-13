// <copyright file="CastleSiegeNpcListPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.CastleSiege;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.CastleSiege;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="ICastleSiegeNpcListPlugIn"/>
/// which forwards defense-structure information to the game client.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CastleSiegeNpcListPlugIn_Name), Description = nameof(PlugInResources.CastleSiegeNpcListPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("FF326700-AB94-44BB-AF03-A3068C28004B")]
public class CastleSiegeNpcListPlugIn : ICastleSiegeNpcListPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastleSiegeNpcListPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public CastleSiegeNpcListPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc />
    public async ValueTask ShowNpcListAsync(IReadOnlyList<CastleSiegeNpcInfo> npcs)
    {
        if (this._player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            var size = CastleSiegeNpcListRef.GetRequiredSize(npcs.Count);
            var packet = new CastleSiegeNpcListRef(connection.Output.GetSpan(size)[..size])
            {
                Result = 1,
                NpcCount = checked((uint)npcs.Count),
            };

            for (var i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                var entry = packet[i];
                entry.NpcNumber = npc.NpcNumber;
                entry.NpcIndex = npc.NpcIndex;
                entry.DefenseUpgradeLevel = npc.DefenseLevel;
                entry.RegenerationLevel = npc.RegenerationLevel;
                entry.MaxHp = checked((uint)npc.MaximumHealth);
                entry.CurrentHp = checked((uint)npc.CurrentHealth);
                entry.PositionX = npc.PositionX;
                entry.PositionY = npc.PositionY;
                entry.IsAlive = npc.IsAlive;
            }

            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
