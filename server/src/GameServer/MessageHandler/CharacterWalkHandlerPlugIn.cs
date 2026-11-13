// <copyright file="CharacterWalkHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic.Views.World;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for walk packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.CharacterWalkHandlerPlugIn_Name), Description = nameof(PlugInResources.CharacterWalkHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("19056DEB-4321-4D25-8615-EE49A453DF03")]
[MinimumClient(1, 0, ClientLanguage.Invariant)]
internal class CharacterWalkHandlerPlugIn : CharacterWalkBaseHandlerPlugIn
{
    /// <inheritdoc/>
    public override byte Key => WalkRequest.Code;
}