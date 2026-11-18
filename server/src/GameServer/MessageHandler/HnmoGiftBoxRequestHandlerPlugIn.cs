// <copyright file="HnmoGiftBoxRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.GiftBox;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>Handles correlated own-account Gift Box / Mail requests (<c>F5 27</c>); identity comes solely from the game session.</summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoGiftBoxRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoGiftBoxRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("CD342E98-F86E-4983-AE90-0E11A5376D3E")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoGiftBoxRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, MaxDepth = 4 };

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoGiftBoxRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length is < 10 or > 255 || player.Account is null || player.SelectedCharacter is null)
        {
            return;
        }

        var message = new HnmoGiftBoxRequest(packet);
        var requestId = message.RequestId;
        if (requestId == 0)
        {
            return;
        }

        WirePayload? wire;
        try
        {
            wire = JsonSerializer.Deserialize<WirePayload>(message.Json, JsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (wire is null)
        {
            return;
        }

        Guid? letterId = null;
        if (!string.IsNullOrWhiteSpace(wire.LetterId))
        {
            if (!Guid.TryParse(wire.LetterId, out var parsed) || parsed == Guid.Empty)
            {
                return;
            }

            letterId = parsed;
        }

        // For live collection, character selection is always the currently entered character.
        var action = wire.Action ?? "state";
        var request = new GiftBoxRequest(
            action,
            wire.Offset,
            letterId,
            action == "collect" ? HNMO.Persistence.ObjectExtensions.GetId(player.SelectedCharacter) : null,
            wire.Language ?? "en");

        if (!request.IsValid)
        {
            return;
        }

        var actions = new GiftBoxGameActions();
        if (await actions.HandleAsync(player, request).ConfigureAwait(false) is { } json)
        {
            await player.InvokeViewPlugInAsync<IHnmoGiftBoxViewPlugIn>(view => view.ShowGiftBoxAsync(requestId, json)).ConfigureAwait(false);
        }
    }

    private sealed class WirePayload
    {
        public string? Action { get; set; }

        public int Offset { get; set; }

        public string? LetterId { get; set; }

        public string? Language { get; set; }
    }
}
