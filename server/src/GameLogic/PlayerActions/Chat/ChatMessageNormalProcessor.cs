// <copyright file="ChatMessageNormalProcessor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Chat;

using HNMO.GameLogic.Views;

/// <summary>
/// A chat message processor for normal chat.
/// </summary>
public class ChatMessageNormalProcessor : BannableChatMessageBaseProcessor
{
    /// <inheritdoc/>
    public override async ValueTask SubclassProcessMessageAsync(Player sender, (string Message, string PlayerName) content)
    {
        sender.Logger.LogDebug("Sending Chat Message to Observers, Count: {0}", sender.Observers.Count);
        await sender.ForEachWorldObserverAsync<IChatViewPlugIn>(p => p.ChatMessageAsync(content.Message, sender.SelectedCharacter!.Name, ChatMessageType.Normal), true).ConfigureAwait(false);
        await sender.NotifyChatMessageSentAsync(content.Message, ChatMessageType.Normal).ConfigureAwait(false);
    }
}