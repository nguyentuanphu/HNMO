// <copyright file="ContractChatCommandPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Escrow;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.PlugIns;

/// <summary>Chat command <c>/contract [buyer]</c> opening the Escrow Contracts window (<c>F9</c>).</summary>
[Guid("1F8E2A3B-4C5D-4E6F-8A9B-0C1D2E3F4A5B")]
[PlugIn]
[Display(Name = "Escrow Contract Command (/contract)", Description = "Opens the Escrow Contracts window, optionally pre-filling a named buyer.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class ContractChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/contract";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (EscrowContractsPlugIn.Get(player.GameContext) is not { } plugin)
        {
            var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
            await player.ShowBlueMessageAsync(isVi ? "Hệ thống hợp đồng ký gửi chưa được bật." : "Escrow contracts are disabled on this server.").ConfigureAwait(false);
            return;
        }

        var target = command.Length > Command.Length ? command[Command.Length..].Trim() : null;
        await plugin.HandlePacketAsync(player, 0, null, targetPrefill: target).ConfigureAwait(false);
    }
}
