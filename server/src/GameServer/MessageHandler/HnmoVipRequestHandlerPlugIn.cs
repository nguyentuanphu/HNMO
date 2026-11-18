// <copyright file="HnmoVipRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>Parses bounded VIP selections; identity comes exclusively from the authenticated game session.</summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HnmoVipRequestHandlerPlugIn_Name), Description = nameof(PlugInResources.HnmoVipRequestHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("740F2142-8A90-4B96-881C-C7795B6A9BF4")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoVipRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, MaxDepth = 4 };

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoVipRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length is < 10 or > 255 || player.Account is null || player.SelectedCharacter is null)
        {
            return;
        }

        var message = new HnmoVipRequest(packet);
        var requestId = message.RequestId;
        VipGameRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<VipGameRequest>(message.Json, JsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (requestId == 0 || request?.IsValid != true)
        {
            return;
        }

        var actions = VipPlugIn.Get(player.GameContext)?.CreateActions() ?? new VipGameActions(new(), VipStateStore.OpenHost());
        if (await actions.HandleAsync(player, request).ConfigureAwait(false) is { } json)
        {
            await player.InvokeViewPlugInAsync<IHnmoVipViewPlugIn>(view => view.ShowVipAsync(requestId, json)).ConfigureAwait(false);
        }
    }
}
