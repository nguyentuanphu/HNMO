// <copyright file="ChatMessageWhisperProcessor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Chat;

using System.ComponentModel;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views;

/// <summary>
/// A chat message processor which sends the message to the whisper receiver.
/// </summary>
public class ChatMessageWhisperProcessor : BannableChatMessageBaseProcessor
{
    /// <inheritdoc />
    public override async ValueTask SubclassProcessMessageAsync(Player sender, (string Message, string PlayerName) content)
    {
        var whisperReceiver = sender.GameContext.GetPlayerByCharacterName(content.PlayerName);
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