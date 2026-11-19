// <copyright file="ChatLogEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using HNMO.GameLogic.Views;

/// <summary>
/// A chat line in the <see cref="ChatLog"/>.
/// </summary>
/// <param name="Id">The running number of the line.</param>
/// <param name="TimeUtc">When it was sent (UTC).</param>
/// <param name="ServerId">The id of the game server.</param>
/// <param name="MapName">The name of the sender's map.</param>
/// <param name="Sender">The sender's character name.</param>
/// <param name="AccountId">The id of the sender's account.</param>
/// <param name="Kind">The kind of the message.</param>
/// <param name="Receiver">The receiver of a whisper.</param>
/// <param name="Text">The text.</param>
public sealed record ChatLogEntry(long Id, DateTime TimeUtc, int ServerId, string MapName, string Sender, Guid AccountId, ChatMessageType Kind, string? Receiver, string Text);
