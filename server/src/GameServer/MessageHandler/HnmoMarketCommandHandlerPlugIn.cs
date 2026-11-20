// <copyright file="HnmoMarketCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.ItemMarket;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Handles <see cref="HnmoMarketCommand"/> packets (<c>F5 38</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Item Market handler", Description = "Handles item market requests, escrow listing, atomic purchases, cancellations, and claims.")]
[Guid("E16C5D0B-9A43-4F27-98B2-7D31E0C4A5F8")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal sealed class HnmoMarketCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoMarketCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var command = new HnmoMarketCommand(packet);
        var plugIn = MarketPlugIn.Get(player.GameContext);
        if (plugIn is null)
        {
            if (command.Action == HnmoMarketCommand.HnmoMarketActionType.Request)
            {
                var offEngine = new MarketEngine(new MarketConfiguration(), MarketStore.Open(null), enabled: false);
                var offJson = MarketStateWriter.WritePublic(offEngine, DateTime.UtcNow);
                await player.InvokeViewPlugInAsync<IHnmoMarketViewPlugIn>(p => p.ShowMarketAsync(offJson, requested: true)).ConfigureAwait(false);
            }

            return;
        }

        switch (command.Action)
        {
            case HnmoMarketCommand.HnmoMarketActionType.Request:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
            case HnmoMarketCommand.HnmoMarketActionType.List:
                await plugIn.ListItemAsync(player, command.Slot, (MarketCurrency)command.Currency, unchecked((int)command.Price)).ConfigureAwait(false);
                break;
            case HnmoMarketCommand.HnmoMarketActionType.Buy:
                await plugIn.BuyAsync(player, command.ListingId).ConfigureAwait(false);
                break;
            case HnmoMarketCommand.HnmoMarketActionType.Cancel:
                await plugIn.CancelAsync(player, command.ListingId).ConfigureAwait(false);
                break;
            case HnmoMarketCommand.HnmoMarketActionType.Claim:
                await plugIn.ClaimAsync(player).ConfigureAwait(false);
                break;
            case HnmoMarketCommand.HnmoMarketActionType.PayAccess:
                await plugIn.PayAccessAsync(player).ConfigureAwait(false);
                break;
        }
    }
}
