// <copyright file="MineChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.PlugIns;

/// <summary>Chat command <c>/mine</c> to mine twinkle ore nodes with Pandora Pick or repair with Jewel of Bless.</summary>
[Guid("6C8E3A29-9B41-4E22-A315-7D2E4F5A9C22")]
[PlugIn]
[Display(Name = "Mining System Chat Command (/mine)", Description = "Mines crystal twinkles with Pandora Pick (/mine [small|medium|large] [stage] or /mine repair).")]
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
        if (S6EventsPlugIn.Get(player.GameContext) is not { } plugin)
        {
            var isVi = string.Equals(player.Culture.TwoLetterISOLanguageName, "vi", StringComparison.OrdinalIgnoreCase);
            await player.ShowBlueMessageAsync(isVi ? "Hệ thống Đào Khoáng chưa được bật." : "Mining System is disabled on this server.").ConfigureAwait(false);
            return;
        }

        await plugin.HandleMineCommandAsync(player, command).ConfigureAwait(false);
    }
}
