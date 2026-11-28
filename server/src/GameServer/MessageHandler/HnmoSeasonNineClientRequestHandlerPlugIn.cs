// <copyright file="HnmoSeasonNineClientRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.SeasonNineSystems;
using HNMO.GameServer.Properties;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoSeasonNineClientRequest"/> (<c>F5 58</c>): answers with the realm's Season 9 client
/// settings, or nothing outside an active Season 9 realm.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoSeasonNineClientRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoSeasonNineClientRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6E1A9C37-D58B-4F02-A4E6-2B7F0D93C518")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoSeasonNineClientRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoSeasonNineClientRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || SeasonNineSystemsPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        await plugIn.SendClientStateAsync(player).ConfigureAwait(false);
    }
}
