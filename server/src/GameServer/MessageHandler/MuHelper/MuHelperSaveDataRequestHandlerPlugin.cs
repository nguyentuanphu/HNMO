// <copyright file="MuHelperSaveDataRequestHandlerPlugin.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler.MuHelper;

using System.Buffers;
using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.MuHelper;
using HNMO.GameServer.RemoteView.MuHelper;
using HNMO.Network.Packets.ClientToServer;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for mu helper data save packets (0xAE identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.MuHelperSaveDataRequestHandlerPlugin_Name), Description = nameof(PlugInResources.MuHelperSaveDataRequestHandlerPlugin_Description), ResourceType = typeof(PlugInResources))]
[Guid("493B12F2-5115-4587-B0CF-B1E4F9B77249")]
public class MuHelperSaveDataRequestHandlerPlugin : IPacketHandlerPlugIn
{
    private readonly UpdateMuHelperConfigurationAction _updateMuBotConfigurationAction = new();

    /// <inheritdoc />
    public byte Key => MuHelperSaveDataRequest.Code;

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        MuHelperSaveDataRequest message = packet;
        var dataSize = message.HelperData.Length;
        using var memoryOwner = MemoryPool<byte>.Shared.Rent(dataSize);
        var memory = memoryOwner.Memory[..dataSize];
        message.HelperData.CopyTo(memory.Span);
        await this._updateMuBotConfigurationAction.SaveDataAsync(player, memory).ConfigureAwait(false);
        if (player.SelectedCharacter?.MuHelperConfiguration is { } configuration)
        {
            player.MuHelperSettings = MuHelperSettingsSerializer.TryDeserialize(configuration);
        }
    }
}