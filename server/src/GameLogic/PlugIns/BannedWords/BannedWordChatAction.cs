// <copyright file="BannedWordChatAction.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
