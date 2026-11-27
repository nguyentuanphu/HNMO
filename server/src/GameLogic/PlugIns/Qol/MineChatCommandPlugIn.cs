// <copyright file="MineChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/mine</c> excavating minerals, Zen, or jewels with cooldown &amp; optional pick requirement.</summary>
[Guid("9F3C4D5E-6A7B-4C8D-0E9F-1A2B3C4D5E6F")]
[PlugIn]
[Display(Name = "Mining Command (/mine)", Description = "Excavates Zen or jewels cleanly with cooldown and optional pick requirement.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class MineChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/mine";

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
            await player.ShowBlueMessageAsync(isVi ? "Tính năng đào khoáng (/mine) chưa được bật." : "Mining (/mine) is disabled on this server.").ConfigureAwait(false);
            return;
        }

        await plugin.HandleMineCommandAsync(player).ConfigureAwait(false);
    }
}
