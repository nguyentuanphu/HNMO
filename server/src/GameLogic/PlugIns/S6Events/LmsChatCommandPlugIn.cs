// <copyright file="LmsChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/lms</c> allowing players to join or check Last Man Standing event status.</summary>
[Guid("2D1B8F33-5A6C-4C12-901F-6B7D8E9A0123")]
[PlugIn]
[Display(Name = "LMS Chat Command (/lms)", Description = "Joins or checks the Last Man Standing arena event.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class LmsChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/lms";

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
            await player.ShowBlueMessageAsync(isVi ? "Sự kiện LMS chưa được bật trên máy chủ." : "Last Man Standing is disabled on this server.").ConfigureAwait(false);
            return;
        }

        await plugin.HandleLmsCommandAsync(player).ConfigureAwait(false);
    }
}
