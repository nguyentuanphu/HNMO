// <copyright file="ChatLogPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using HNMO.GameLogic.Views;
using HNMO.PlugIns;

/// <summary>
/// Keeps the last public, party, guild, alliance, world and whisper lines in the <see cref="ChatLog"/>,
/// which the admin panel shows ("Chat log").
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ChatLogPlugIn_Name), Description = nameof(PlugInResources.ChatLogPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("5E91B2D4-7C38-4A06-8F1E-B43D6A2C9E70")]
public class ChatLogPlugIn : IChatMessageSentPlugIn
{
    /// <summary>
    /// Adds a line of a player to the chat log, if the plug-in is active.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="kind">The kind of the message.</param>
    /// <param name="receiverName">The receiver of a whisper.</param>
    /// <param name="message">The message.</param>
    public static void Record(Player sender, ChatMessageType kind, string? receiverName, string message)
    {
        if (sender.GameContext.PlugInManager.GetActivePlugInsOf<IChatMessageSentPlugIn>().OfType<ChatLogPlugIn>().Any())
        {
            Add(sender, kind, receiverName, message);
        }
    }

    /// <inheritdoc />
    public ValueTask ChatMessageSentAsync(Player sender, string message, ChatMessageType messageType, Player? receiver)
    {
        if (messageType is ChatMessageType.Command)
        {
            return ValueTask.CompletedTask;
        }

        Add(sender, messageType, receiver?.SelectedCharacter?.Name, message);
        return ValueTask.CompletedTask;
    }

    private static void Add(Player sender, ChatMessageType kind, string? receiverName, string message)
    {
        if (sender.SelectedCharacter is not { } character)
        {
            return;
        }

        var serverId = sender.GameContext is IGameServerContext serverContext ? serverContext.Id : 0;
        var mapName = sender.CurrentMap?.Definition.Name.ToString() ?? string.Empty;
        ChatLog.Shared.Add(DateTime.UtcNow, serverId, mapName, character.Name, sender.Account is { } account ? HNMO.Persistence.ObjectExtensions.GetId(account) : Guid.Empty, kind, receiverName, message);
    }
}
