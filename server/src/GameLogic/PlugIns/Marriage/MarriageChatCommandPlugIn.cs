// <copyright file="MarriageChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Marriage;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.GameLogic.Views;
using HNMO.Interfaces;
using HNMO.PlugIns;

/// <summary>Handles <c>/marry</c> commands in-game.</summary>
[PlugIn]
[Display(Name = "Marriage chat command (/marry)", Description = "Propose, accept, decline, warp to spouse, change couple title, or divorce via /marry.")]
[Guid("8D5A4C1F-9B02-4E63-8F25-19C4A7E2D608")]
[ChatCommandHelp(Command, typeof(MarriageChatCommandArgs), CharacterStatus.Normal)]
public sealed class MarriageChatCommandPlugIn : ChatCommandPlugInBase<MarriageChatCommandArgs>
{
    private const string Command = "/marry";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player player, MarriageChatCommandArgs arguments)
    {
        if (MarriagePlugIn.Get(player.GameContext) is not { } plugIn)
        {
            var off = player.Culture.TwoLetterISOLanguageName == "vi"
                ? "Tính năng Kết hôn hiện đang tắt."
                : "Marriage is currently turned off.";
            await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(off, MessageType.BlueNormal)).ConfigureAwait(false);
            return;
        }

        var first = arguments.Target?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(first))
        {
            await plugIn.SendStateAsync(player, requested: true).ConfigureAwait(false);
            return;
        }

        if (string.Equals(first, "accept", StringComparison.OrdinalIgnoreCase))
        {
            await plugIn.AcceptAsync(player).ConfigureAwait(false);
            return;
        }

        if (string.Equals(first, "decline", StringComparison.OrdinalIgnoreCase))
        {
            await plugIn.DeclineAsync(player).ConfigureAwait(false);
            return;
        }

        if (string.Equals(first, "divorce", StringComparison.OrdinalIgnoreCase))
        {
            await plugIn.DivorceAsync(player).ConfigureAwait(false);
            return;
        }

        if (string.Equals(first, "warp", StringComparison.OrdinalIgnoreCase))
        {
            await plugIn.TeleportToSpouseAsync(player).ConfigureAwait(false);
            return;
        }

        if (string.Equals(first, "title", StringComparison.OrdinalIgnoreCase))
        {
            await plugIn.SetTitleAsync(player, arguments.Value ?? string.Empty).ConfigureAwait(false);
            return;
        }

        await plugIn.ProposeAsync(player, first).ConfigureAwait(false);
    }
}
