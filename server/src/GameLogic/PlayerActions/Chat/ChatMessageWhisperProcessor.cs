// <copyright file="ChatMessageWhisperProcessor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Chat;

using System.ComponentModel;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Chat;
using HNMO.GameLogic.Views;

/// <summary>
/// A chat message processor which sends the message to the whisper receiver.
/// </summary>
public class ChatMessageWhisperProcessor : BannableChatMessageBaseProcessor
{
    /// <inheritdoc />
    public override async ValueTask SubclassProcessMessageAsync(Player sender, (string Message, string PlayerName) content)
    {
        // The receiver may be on another game server of this process.
        var whisperReceiver = sender.GameContext.GetPlayerByCharacterName(content.PlayerName)
                              ?? OnlineCharacters.Get(content.PlayerName);
        if (whisperReceiver is null)
        {
            // A friend playing in another HNMO program (Season 6.3, Season 9 or the Arena) gets it there.
            if (await PlugIns.Chat.CrossSeasonFriendsPlugIn.TrySendAsync(sender, content.PlayerName, content.Message).ConfigureAwait(false))
            {
                ChatLogPlugIn.Record(sender, ChatMessageType.Whisper, content.PlayerName, content.Message);
                return;
            }

            // Kept until the receiver enters the game, if the offline whispers are active.
            if (OfflineWhisperPlugIn.Get(sender.GameContext) is { } offlineWhispers
                && await offlineWhispers.StoreAsync(sender, content.PlayerName, content.Message).ConfigureAwait(false))
            {
                ChatLogPlugIn.Record(sender, ChatMessageType.Whisper, content.PlayerName, content.Message);
            }

            return;
        }

        if (whisperReceiver != null)
        {
            var eventArgs = new CancelEventArgs();
            sender.GameContext.PlugInManager.GetPlugInPoint<IWhisperMessageReceivedPlugIn>()?.WhisperMessageReceived(sender, whisperReceiver, content.Message, eventArgs);
            if (!eventArgs.Cancel)
            {
                await whisperReceiver.InvokeViewPlugInAsync<IChatViewPlugIn>(p => p.ChatMessageAsync(content.Message, sender.SelectedCharacter!.Name, ChatMessageType.Whisper)).ConfigureAwait(false);
                await sender.NotifyChatMessageSentAsync(content.Message, ChatMessageType.Whisper, whisperReceiver).ConfigureAwait(false);
            }
        }
    }
}