// <copyright file="HnmoPartyOptionsCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.PartyOptions;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoPartyOptionsCommand"/> (<c>F5 44</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Party Options Command Handler", Description = "Handles party password & auto-party hunting spot actions.")]
[Guid("4A7E2D81-5B90-4C63-9F14-6E2A8B7C3012")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoPartyOptionsCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoPartyOptionsCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || PartyOptionsPlugIn.Get(player.GameContext) is not { } plugIn)
        {
            return;
        }

        HnmoPartyOptionsCommand cmd = packet;
        var args = ParsePayload(cmd.Payload);
        switch (cmd.Action)
        {
            case 1:
                await plugIn.AdvertiseAsync(player, args.Spot, args.Password, args.MinLevel, args.MaxLevel).ConfigureAwait(false);
                break;
            case 2:
                await plugIn.HandlePartyPassCommandAsync(player, args.Password).ConfigureAwait(false);
                break;
            case 3:
                await plugIn.JoinLeaderPartyAsync(player, args.Leader, args.Password).ConfigureAwait(false);
                break;
            case 4:
                await plugIn.StopAdvertisingAsync(player).ConfigureAwait(false);
                break;
            default:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
        }
    }

    private static (string Password, string Spot, string Leader, int MinLevel, int MaxLevel) ParsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (string.Empty, string.Empty, string.Empty, 1, 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            return (
                r.TryGetProperty("password", out var p) ? p.GetString() ?? string.Empty : string.Empty,
                r.TryGetProperty("spot", out var s) ? s.GetString() ?? string.Empty : string.Empty,
                r.TryGetProperty("leader", out var l) ? l.GetString() ?? string.Empty : string.Empty,
                r.TryGetProperty("minLevel", out var mn) && mn.TryGetInt32(out var minV) ? minV : 1,
                r.TryGetProperty("maxLevel", out var mx) && mx.TryGetInt32(out var maxV) ? maxV : 0);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty, string.Empty, 1, 0);
        }
    }
}
