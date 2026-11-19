// <copyright file="OfflineWhisper.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// A whisper to a character which wasn't in the game.
/// </summary>
/// <param name="Sender">The name of the sending character.</param>
/// <param name="Receiver">The name of the receiving character.</param>
/// <param name="Text">The text.</param>
/// <param name="SentUtc">When it was sent (UTC).</param>
public sealed record OfflineWhisper(string Sender, string Receiver, string Text, DateTime SentUtc);
