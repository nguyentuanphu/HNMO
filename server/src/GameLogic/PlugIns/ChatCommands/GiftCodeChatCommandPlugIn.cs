// <copyright file="GiftCodeChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using HNMO.GameLogic.PlugIns.ChatCommands.Arguments;
using HNMO.GameLogic.PlugIns.GiftCodes;
using HNMO.PlugIns;

/// <summary>
/// A chat command plugin which enters a gift code of HNMO's gift codes: <c>/code CODE</c>. The answer comes in the chat
/// (English or Vietnamese).
/// </summary>
[Guid("A3D84E26-71B9-4F0C-9E52-C68B1D07F3A4")]
[PlugIn]
[Display(Name = nameof(PlugInResources.GiftCodeChatCommandPlugIn_Name), Description = nameof(PlugInResources.GiftCodeChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(Command, typeof(GiftCodeChatCommandArgs), CharacterStatus.Normal)]
public class GiftCodeChatCommandPlugIn : ChatCommandPlugInBase<GiftCodeChatCommandArgs>
{
    private const string Command = "/code";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc/>
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public override async ValueTask HandleCommandAsync(Player player, string command)
    {
        try
        {
            // The code is everything after the command; it isn't parsed like other arguments (it may hold any letters).
            var text = command.Length > Command.Length ? command[Command.Length..].Trim() : string.Empty;
            await this.HandleAsync(player, text).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Unexpected error handling the chat command '{Command}'.", Command);
        }
    }

    /// <inheritdoc />
    protected override ValueTask DoHandleCommandAsync(Player player, GiftCodeChatCommandArgs arguments) => this.HandleAsync(player, arguments.Code ?? string.Empty);

    private async ValueTask HandleAsync(Player player, string text)
    {
        var vietnamese = player.Culture.TwoLetterISOLanguageName == "vi";
        if (GiftCodesPlugIn.Get(player.GameContext) is not { } giftCodes)
        {
            await player.ShowBlueMessageAsync(vietnamese ? "Máy chủ này chưa bật gift code." : "Gift codes aren't on for this server.").ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await player.ShowBlueMessageAsync(vietnamese ? "Cách dùng: /code MÃ" : "Usage: /code CODE").ConfigureAwait(false);
            return;
        }

        await giftCodes.RedeemAsync(player, text, false).ConfigureAwait(false);
    }
}
