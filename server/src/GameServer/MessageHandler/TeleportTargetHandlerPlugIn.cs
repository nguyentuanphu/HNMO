// <copyright file="TeleportTargetHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Pathfinding;
using HNMO.PlugIns;

/// <summary>
/// Handler for teleport target packets (teleport ally skill).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.TeleportTargetHandlerPlugIn_Name), Description = nameof(PlugInResources.TeleportTargetHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("279881F9-0AE9-4EDA-8EB1-34D99D3243CC")]
internal class TeleportTargetHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly WizardTeleportAction _teleportAction = new();

    /// <inheritdoc />
    public byte Key => TeleportTargetRef.Code;

    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        TeleportTarget message = packet;

        await this._teleportAction.TryTeleportTargetWithSkillAsync(
            player,
            message.TargetId,
            new Point(message.TeleportTargetX, message.TeleportTargetY)).ConfigureAwait(false);
    }
}