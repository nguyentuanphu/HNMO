// <copyright file="SrvInfoChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Qol;

using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.PlugIns;

/// <summary>Chat command <c>/srvinfo</c> printing live rates, caps, coins, and QoL status.</summary>
[Guid("8E2B3C4D-5F6A-4B7C-9D8E-0F1A2B3C4D5E")]
[PlugIn]
[Display(Name = "Server Info Command (/srvinfo)", Description = "Shows live server XP/drop/Zen rates, caps, and account coins.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class SrvInfoChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/srvinfo";

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
            await player.ShowBlueMessageAsync(isVi ? "Lệnh /srvinfo chưa được bật." : "Server info command is disabled on this server.").ConfigureAwait(false);
            return;
        }

        await plugin.HandleServerInfoCommandAsync(player).ConfigureAwait(false);
    }
}
