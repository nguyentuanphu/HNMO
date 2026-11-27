// <copyright file="LorenChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/loren</c> checking Loren Deep assault status.</summary>
[Guid("5F4B2C18-7A3D-48B9-92E1-4C5D6E7F8A11")]
[PlugIn]
[Display(Name = "Loren Deep Chat Command (/loren)", Description = "Checks Loren Deep assault schedule and wave status.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class LorenChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/loren";

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
            await player.ShowBlueMessageAsync(isVi ? "Sự kiện Loren Deep chưa được bật." : "Loren Deep is disabled on this server.").ConfigureAwait(false);
            return;
        }

        await plugin.HandleLorenCommandAsync(player).ConfigureAwait(false);
    }
}
