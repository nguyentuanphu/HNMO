// <copyright file="XMasChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/xmas</c> to claim Santa Village blessings and gifts.</summary>
[Guid("8B2E0A55-4D63-4192-9F73-0B4C5D6E7F44")]
[PlugIn]
[Display(Name = "XMas Chat Command (/xmas)", Description = "Claims Santa Village Christmas blessings or Snowman seasonal gifts.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class XMasChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/xmas";

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
            await player.ShowBlueMessageAsync(isVi ? "Sự kiện Giáng Sinh chưa được bật." : "XMas Attack is disabled on this server.").ConfigureAwait(false);
            return;
        }

        await plugin.HandleXMasCommandAsync(player).ConfigureAwait(false);
    }
}
