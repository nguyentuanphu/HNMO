// <copyright file="HnmoArenaTravelRequestHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using System.Text.Json;
using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoArenaTravelRequest"/> (<c>F5 52</c>): tells whether this server is the HNMO Arena and, for
/// travel, gives a one-time login ticket (kept in the shared database, so the Arena or the player's home server takes
/// it). The web client then logs in on the other side with it; nothing of the character goes along.
/// </summary>
[PlugIn]
[Display(Name = "HNMO Arena travel handler", Description = "Gives one-time tickets to travel to the HNMO Arena and back home.")]
[Guid("D4B71E2A-5C96-4F83-9E0B-6A3F8C1D7E25")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoArenaTravelRequestHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoArenaTravelRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null || player.Account?.LoginName is not { Length: > 0 } login)
        {
            return;
        }

        var request = new HnmoArenaTravelRequest(packet);
        var vi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
        var inArena = HnmoSeasonInstance.IsArena;
        string ticket = string.Empty;
        string? message = null;
        if (request.Action == 1)
        {
            if (Persistence.SharedLoginTicketStore.Current is not { } shared)
            {
                message = vi ? "Đấu trường chưa mở trên máy chủ này." : "The Arena isn't available on this server.";
            }
            else
            {
                (ticket, _) = await WebLoginTicketStore.Shared.IssueAsync(login, shared).ConfigureAwait(false);
                await player.SaveProgressAsync().ConfigureAwait(false);
                message = inArena
                    ? (vi ? "Đang về máy chủ của bạn..." : "Going back to your server...")
                    : (vi ? "Đang tới Đấu trường..." : "Going to the Arena...");
            }
        }

        var arena = inArena ? GameLogic.PlugIns.Arena.ArenaPlugIn.Get(player.GameContext) : null;
        if (arena is not null && request.Action is 2 or 3)
        {
            var (en, viText) = request.Action == 2
                ? arena.Duels.Join(player)
                : (arena.Duels.Leave(player) ? ("You left the 1v1 queue.", "Bạn đã rời hàng chờ 1v1.") : ("You weren't in the 1v1 queue.", "Bạn không ở trong hàng chờ 1v1."));
            message = vi ? viText : en;
        }

        var shop = inArena ? null : GameLogic.PlugIns.Arena.ArenaShopPlugIn.Get(player.GameContext);
        if (shop is not null && request.Action == 4)
        {
            var (en, viText) = await shop.BuyAsync(player, request.Index).ConfigureAwait(false);
            message = vi ? viText : en;
        }

        var json = JsonSerializer.Serialize(new { inArena, account = login, ticket, message, ranked = arena?.Duels.StateOf(player), shop = shop?.StateOf(player) });
        await player.InvokeViewPlugInAsync<IHnmoArenaTravelViewPlugIn>(p => p.ShowArenaTravelAsync(json)).ConfigureAwait(false);
    }
}
