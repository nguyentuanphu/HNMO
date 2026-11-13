// <copyright file="GuildMasterAnswerHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.Guild;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Guild;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Handler for guild master answer packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.GuildMasterAnswerHandlerPlugIn_Name), Description = nameof(PlugInResources.GuildMasterAnswerHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3715c03e-9c77-4e43-9f6b-c1db3a2c3233")]
internal class GuildMasterAnswerHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly GuildMasterAnswerAction _answerAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => GuildMasterAnswer.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        GuildMasterAnswer answer = packet;
        await this._answerAction.ProcessAnswerAsync(player, answer.ShowCreationDialog ? GuildMasterAnswerAction.Answer.ShowDialog : GuildMasterAnswerAction.Answer.Cancel).ConfigureAwait(false);
    }
}