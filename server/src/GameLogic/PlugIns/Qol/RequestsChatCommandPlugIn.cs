// <copyright file="RequestsChatCommandPlugIn.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Qol;

using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.PlugIns;

/// <summary>Chat command <c>/requests [on|off]</c> toggling automatic refusal of trade, party, and duel invites.</summary>
[Guid("7D1A2B3C-4E5F-4A6B-8C7D-9E0F1A2B3C4D")]
[PlugIn]
[Display(Name = "Requests Toggle Command (/requests)", Description = "Toggles auto-refusal for incoming trade, party, and duel invites.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class RequestsChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/requests";

    /// <inheritdoc />
    public string Key => Command;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        if (QolPlugIn.Get(player.GameContext) is not { } plugin)
        {
            var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
            await player.ShowBlueMessageAsync(isVi ? "Tính năng QoL chưa được bật." : "QoL commands are disabled on this server.").ConfigureAwait(false);
            return;
        }

        var arg = command.Length > Command.Length ? command[Command.Length..].Trim() : string.Empty;
        await plugin.HandleRequestsCommandAsync(player, arg).ConfigureAwait(false);
    }
}
