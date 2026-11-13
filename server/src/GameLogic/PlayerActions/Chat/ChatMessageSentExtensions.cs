// <copyright file="ChatMessageSentExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Chat;

using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.Views;

/// <summary>
/// Extensions to notify the <see cref="IChatMessageSentPlugIn"/>s.
/// </summary>
internal static class ChatMessageSentExtensions
{
    /// <summary>
    /// Notifies the <see cref="IChatMessageSentPlugIn"/>s that a chat message of the player has been delivered.
    /// </summary>
    /// <param name="sender">The sending player.</param>
    /// <param name="message">The delivered message.</param>
    /// <param name="messageType">The type of the message.</param>
    /// <param name="receiver">The receiver of a whisper message.</param>
    public static async ValueTask NotifyChatMessageSentAsync(this Player sender, string message, ChatMessageType messageType, Player? receiver = null)
    {
        if (sender.GameContext.PlugInManager.GetPlugInPoint<IChatMessageSentPlugIn>() is { } plugInPoint)
        {
            await plugInPoint.ChatMessageSentAsync(sender, message, messageType, receiver).ConfigureAwait(false);
        }
    }
}
