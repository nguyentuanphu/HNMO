// <copyright file="CharacterMoveHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for move packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CharacterMoveHandlerPlugIn_Name), Description = nameof(PlugInResources.CharacterMoveHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("d3b04177-131f-4bf5-a228-1f10d22d54f2")]
[MinimumClient(0, 97, ClientLanguage.English)]
internal class CharacterMoveHandlerPlugIn : CharacterMoveBaseHandlerPlugIn
{
    /// <inheritdoc/>
    public override byte Key => InstantMoveRequest.Code;
}