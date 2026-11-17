// <copyright file="HnmoCustomRanksCommandHandlerPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns.CustomRanks;
using HNMO.GameLogic.Views;
using HNMO.Network.Packets.ClientToServer;
using HNMO.Network.PlugIns;
using HNMO.PlugIns;

/// <summary>
/// Packet handler for <see cref="HnmoCustomRanksCommand"/> (<c>F5 48</c>).
/// </summary>
[PlugIn]
[Display(Name = "HNMO Custom Ranks Command Handler", Description = "Handles custom title state requests and active title selection.")]
[Guid("D6A13C40-8E2B-4B79-9C42-6F1D0E3A8B92")]
[MinimumClient(106, 3, ClientLanguage.Invariant)]
[BelongsToGroup(ChatCommandGroupHandlerPlugIn.GroupKey)]
internal class HnmoCustomRanksCommandHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => HnmoCustomRanksCommand.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player.SelectedCharacter is null)
        {
            return;
        }

        var plugIn = CustomRanksPlugIn.Get(player.GameContext);
        if (plugIn is null)
        {
            return;
        }

        HnmoCustomRanksCommand cmd = packet;
        switch (cmd.Action)
        {
            case 1:
            {
                var (ok, msgEn, msgVi) = await plugIn.SelectRankAsync(player, cmd.RankId).ConfigureAwait(false);
                var isVi = player.Culture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
                await player.ShowBlueMessageAsync(isVi ? msgVi : msgEn).ConfigureAwait(false);
                break;
            }

            default:
                await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
                break;
        }
    }
}
