// <copyright file="ChatCommandGroupHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging;

/// <summary>
/// Packet handler for the messages about chat commands (0xF5 identifier).
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ChatCommandGroupHandlerPlugIn_Name), Description = nameof(PlugInResources.ChatCommandGroupHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("2F1B5B26-2E22-4E7D-9C8B-1C6E5B7A9D40")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
internal class ChatCommandGroupHandlerPlugIn : GroupPacketHandlerPlugIn
{
    /// <summary>
    /// The group key. It's the <see cref="ChatCommandListRequest.Code"/>, which can't be
    /// used here directly because it's not a compile time constant.
    /// </summary>
    internal const byte GroupKey = 0xF5;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatCommandGroupHandlerPlugIn" /> class.
    /// </summary>
    /// <param name="clientVersionProvider">The client version provider.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public ChatCommandGroupHandlerPlugIn(IClientVersionProvider clientVersionProvider, PlugInManager manager, ILoggerFactory loggerFactory)
        : base(clientVersionProvider, manager, loggerFactory)
    {
    }

    /// <inheritdoc/>
    public override bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public override byte Key => GroupKey;

    /// <inheritdoc/>
    /// <remarks>
    /// While a trade or a crafting dialog is open, the player's inventory and money have a backup which is put
    /// back when the trade is cancelled or the connection ends. An HNMO feature which takes items or money in
    /// that time (market, jewel bank, escrow, mounts, ...) would keep what it gave while the backup returns what
    /// it took. So none of HNMO's own messages (sub codes 2 and up) are handled until the dialog is closed.
    /// </remarks>
    public override async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        const byte firstHnmoSubCode = 2;
        var subTypeIndex = packet.Span[0] % 2 == 1 ? 3 : 4;
        if (player.BackupInventory is not null && packet.Length > subTypeIndex && packet.Span[subTypeIndex] >= firstHnmoSubCode)
        {
            return;
        }

        await base.HandlePacketAsync(player, packet).ConfigureAwait(false);
    }
}
