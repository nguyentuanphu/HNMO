// <copyright file="LuckyCoinChatCommandPlugIn.cs" company="HNMO">
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

/// <summary>Chat command <c>/luckycoin [count]</c> exchanging stacked Lucky Coins for tiered weighted bags.</summary>
[Guid("4F3D0B55-7C8E-4E34-923B-8D9F0A1C2345")]
[PlugIn]
[Display(Name = "Lucky Coin Command (/luckycoin)", Description = "Exchanges stacked Lucky Coins for weighted rewards.")]
[ChatCommandHelp(Command, CharacterStatus.Normal)]
public sealed class LuckyCoinChatCommandPlugIn : IChatCommandPlugIn
{
    private const string Command = "/luckycoin";

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
            await player.ShowBlueMessageAsync(isVi ? "Tính năng đổi Xu May Mắn chưa được bật." : "Lucky Coin exchange is disabled on this server.").ConfigureAwait(false);
            return;
        }

        var arg = command.Length > Command.Length ? command[Command.Length..].Trim() : string.Empty;
        _ = int.TryParse(arg, out var requested);
        await plugin.HandleExchangeCommandAsync(player, isCherryBlossom: false, requested).ConfigureAwait(false);
    }
}
