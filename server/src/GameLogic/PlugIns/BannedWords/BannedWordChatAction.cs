// <copyright file="BannedWordChatAction.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.BannedWords;

/// <summary>
/// Action taken when a chat message contains a banned word or phrase.
/// </summary>
public enum BannedWordChatAction
{
    /// <summary>
    /// Replaces matched banned words with asterisks (<c>*</c>) before delivering the message.
    /// </summary>
    Censor = 0,

    /// <summary>
    /// Drops the message completely and notifies the sender.
    /// </summary>
    Block = 1,
}
