// <copyright file="BlossomChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/blossom [count]</c> exchanging Golden Cherry Blossom Branches for tiered buffs and bags.</summary>
[Guid("3E2C9A44-6B7D-4D23-912A-7C8E9F0B1234")]
[PlugIn]
[Display(Name = "Cherry Blossom Command (/blossom)", Description = "Exchanges Golden Cherry Blossom Branches for rewards.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class BlossomChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/blossom";

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
            await player.ShowBlueMessageAsync(isVi ? "Tính năng đổi Hoa Đào chưa được bật." : "Cherry Blossom exchange is disabled on this server.").ConfigureAwait(false);
            return;
        }

        var arg = command.Length > Command.Length ? command[Command.Length..].Trim() : string.Empty;
        _ = int.TryParse(arg, out var requested);
        await plugin.HandleExchangeCommandAsync(player, isCherryBlossom: true, requested).ConfigureAwait(false);
    }
}
