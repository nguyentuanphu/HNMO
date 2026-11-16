// <copyright file="ChatMessageGlobalNotificationProcessor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Chat;

using System.ComponentModel;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.BannedWords;
using HNMO.GameLogic.Views;

/// <summary>
/// A chat message processor which sends a global notification.
/// </summary>
public class ChatMessageGlobalNotificationProcessor : IChatMessageProcessor
{
    /// <inheritdoc />
    public async ValueTask ProcessMessageAsync(Player sender, (string Message, string PlayerName) content)
    {
        var eventArgs = new CancelEventArgs();
        sender.GameContext.PlugInManager.GetPlugInPoint<IChatMessageReceivedPlugIn>()
            ?.ChatMessageReceived(sender, content.Message, eventArgs);
        if (eventArgs.Cancel)
        {
            return;
        }

        if (sender.SelectedCharacter!.CharacterStatus < CharacterStatus.GameMaster)
        {
            return;
        }

        var (allowed, filteredMessage) = await BannedWordsPlugIn.FilterChatAsync(sender, content.Message).ConfigureAwait(false);
        if (!allowed)
        {
            return;
        }

        await sender.GameContext.SendGlobalNotificationAsync(filteredMessage).ConfigureAwait(false);
        await sender.NotifyChatMessageSentAsync(filteredMessage, ChatMessageType.GlobalNotification).ConfigureAwait(false);
    }
}