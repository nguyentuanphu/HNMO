// <copyright file="AnimationHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.DataModel.Entities;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for animation packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.AnimationHandlerPlugIn_Name), Description = nameof(PlugInResources.AnimationHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5cf7fa95-5ca2-4e14-bb08-4b64250a8ee8")]
internal class AnimationHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => AnimationRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 5)
        {
            return;
        }

        AnimationRequest request = packet;

        var rotation = request.Rotation.ParseAsDirection();
        var animation = request.AnimationNumber;
        player.Rotation = rotation;

        player.Pose = animation switch
        {
            0x80 or 0x6C => CharacterPose.Sitting,
            0x81 or 0x6D => CharacterPose.Leaning,
            0x82 or 0x6E => CharacterPose.Hanging,
            _ => default,
        };

        if (player.Pose > CharacterPose.Standing)
        {
            player.Attributes?.SetStatAttribute(Stats.IsResting, 1.0f);
        }

        await player.ForEachWorldObserverAsync<IShowAnimationPlugIn>(p => p.ShowAnimationAsync(player, animation, null, rotation), false).ConfigureAwait(false);
    }
}