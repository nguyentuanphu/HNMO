// <copyright file="PostChatCommandPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using HNMO.GameLogic.PlugIns.BannedWords;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// A chat command plugin which handles post commands by sending a blue system message to all players.
/// </summary>
[Guid("ED2523C1-F66D-4B53-814E-D2FC0C1F46C0")]
[PlugIn]
[Display(Name = nameof(PlugInResources.PostChatCommandPlugIn_Name), Description = nameof(PlugInResources.PostChatCommandPlugIn_Description), ResourceType = typeof(PlugInResources))]
[ChatCommandHelp(CommandKey, CharacterStatus.Normal)]
public class PostChatCommandPlugIn : IChatCommandPlugIn
{
    private const string CommandKey = "/post";

    /// <inheritdoc />
    public string Key => CommandKey;

    /// <inheritdoc />
    public CharacterStatus MinCharacterStatusRequirement => CharacterStatus.Normal;

    /// <inheritdoc />
    public async ValueTask HandleCommandAsync(Player player, string command)
    {
        var regex = new Regex(Regex.Escape(CommandKey));
        var message = regex.Replace(command, string.Empty, 1)?.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // A muted player can't post to the whole server either, and posts count for the spam guard.
        if (await PlayerActions.Chat.BannableChatMessageBaseProcessor.RefuseMutedOrSpamAsync(player, message).ConfigureAwait(false))
        {
            return;
        }

        var (allowed, filteredMessage) = await BannedWordsPlugIn.FilterChatAsync(player, message).ConfigureAwait(false);
        if (!allowed)
        {
            return;
        }

        message = $"{player.SelectedCharacter?.Name}: {filteredMessage}";
        await player.GameContext.SendGlobalChatMessageAsync("[POST]", message, ChatMessageType.Gens).ConfigureAwait(false);
    }
}