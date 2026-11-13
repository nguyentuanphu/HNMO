// <copyright file="WhisperedChatMessageHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for whispered chat messages.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.WhisperedChatMessageHandlerPlugIn_Name), Description = nameof(PlugInResources.WhisperedChatMessageHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A81C652C-4DAE-477F-A7D0-328924B8A3FC")]
internal class WhisperedChatMessageHandlerPlugIn : ChatMessageBaseHandlerPlugIn
{
    /// <inheritdoc />
    public override byte Key => WhisperMessage.Code;

    /// <inheritdoc />
    protected override bool IsWhisper => true;
}