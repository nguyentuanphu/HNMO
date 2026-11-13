// <copyright file="ChatMessageHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for chat messages.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ChatMessageHandlerPlugIn_Name), Description = nameof(PlugInResources.ChatMessageHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("EDECCEC6-9DC7-499F-8658-EAF94498BDEE")]
internal class ChatMessageHandlerPlugIn : ChatMessageBaseHandlerPlugIn
{
    /// <inheritdoc />
    public override byte Key => PublicChatMessage.Code;

    /// <inheritdoc />
    protected override bool IsWhisper => false;
}