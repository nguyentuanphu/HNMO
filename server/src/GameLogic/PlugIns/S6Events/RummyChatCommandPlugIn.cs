// <copyright file="RummyChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/rummy</c> to combine Card Pieces and play Mu Rummy.</summary>
[Guid("7A1D9F44-3C52-4F81-8E62-9A3B4C5D6E33")]
[PlugIn]
[Display(Name = "Mu Rummy Chat Command (/rummy)", Description = "Plays Mu Rummy mini-game (/rummy combine|start|status|match i j k|finish).")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class RummyChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/rummy";

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
            await player.ShowBlueMessageAsync(isVi ? "Sự kiện Mu Rummy chưa được bật." : "Mu Rummy is disabled on this server.").ConfigureAwait(false);
            return;
        }

        await plugin.HandleRummyCommandAsync(player, command).ConfigureAwait(false);
    }
}
