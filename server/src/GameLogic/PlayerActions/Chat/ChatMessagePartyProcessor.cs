// <copyright file="ChatMessagePartyProcessor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Chat;

using System.ComponentModel;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views;

/// <summary>
/// A chat message processor which sends the message to the party.
/// </summary>
public class ChatMessagePartyProcessor : BannableChatMessageBaseProcessor
{
    /// <inheritdoc />
    public override async ValueTask SubclassProcessMessageAsync(Player sender, (string Message, string PlayerName) content)
    {
        var eventArgs = new CancelEventArgs();
        sender.GameContext.PlugInManager.GetPlugInPoint<IChatMessageReceivedPlugIn>()?.ChatMessageReceived(sender, content.Message, eventArgs);
        if (eventArgs.Cancel)
        {
            return;
        }

        if (sender.Party is { } party)
        {
            await party.SendChatMessageAsync(content.Message, sender.SelectedCharacter!.Name).ConfigureAwait(false);
            await sender.NotifyChatMessageSentAsync(content.Message, ChatMessageType.Party).ConfigureAwait(false);
        }
    }
}