// <copyright file="ObjectMovedPlugInExtended.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.RemoteView.World;

using System.Buffers;
using System.Runtime.InteropServices;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic;
using HNMO.GameLogic.NPC;
using HNMO.GameLogic.Views;
using HNMO.GameLogic.Views.World;
using HNMO.Network;
using HNMO.Network.Packets.ServerToClient;
using HNMO.Network.PlugIns;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IObjectMovedPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ObjectMovedPlugInExtended_Name), Description = nameof(PlugInResources.ObjectMovedPlugInExtended_Description), ResourceType = typeof(PlugInResources))]
[Guid("a56b7400-e51d-4fd6-930b-479c14673719")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
public class ObjectMovedPlugInExtended : ObjectMovedPlugIn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectMovedPlugInExtended"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ObjectMovedPlugInExtended(RemotePlayer player)
        : base(player)
    {
    }

    /// <inheritdoc />
    protected override async ValueTask SendWalkAsync(IConnection connection, ushort objectId, Point sourcePoint, Point targetPoint, Memory<Direction> steps, Direction rotation, int stepsLength)
    {
        int Write()
        {
            var stepsSize = stepsLength == 0 ? 0 : (stepsLength / 2) + 2;
            var size = ObjectWalkedExtended.GetRequiredSize(stepsSize);
            var span = connection.Output.GetSpan(size)[..size];

            var walkPacket = new ObjectWalkedExtendedRef(span)
            {
                HeaderCode = this.GetWalkCode(),
                ObjectId = objectId,
                SourceX = sourcePoint.X,
                SourceY = sourcePoint.Y,
                TargetX = targetPoint.X,
                TargetY = targetPoint.Y,
                TargetRotation = rotation.ToPacketByte(),
                StepCount = (byte)stepsLength,
            };

            this.SetStepData(walkPacket, steps.Span[..stepsLength], stepsSize);
            return size;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    private void SetStepData(ObjectWalkedExtendedRef walkPacket, Span<Direction> steps, int stepsSize)
    {
        if (steps == default || walkPacket.StepCount == 0)
        {
            return;
        }

        walkPacket.StepData[0] = (byte)(steps[0].ToPacketByte() << 4 | stepsSize);
        for (int i = 0; i < stepsSize - 1; i += 2)
        {
            var index = 1 + (i / 2);
            var firstStep = steps[i].ToPacketByte();
            var secondStep = steps.Length > i + 1 ? steps[i + 1].ToPacketByte() : 0;
            walkPacket.StepData[index] = (byte)(firstStep << 4 | secondStep);
        }
    }
}
