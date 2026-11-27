// <copyright file="VipChat.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Vip;

using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.Views;

/// <summary>Marks actual live VIP chat while preserving channel control prefixes and the complete character name.</summary>
public static class VipChat
{
    /// <summary>Formats a message from a server-resolved sender; it never changes recipients or broadcasts a private message.</summary>
    /// <param name="sender">The authoritative live sender.</param>
    /// <param name="message">The existing chat text.</param>
    /// <param name="type">The original channel.</param>
    /// <returns>The same text with a VIP mark when the live timed entitlement is active.</returns>
    public static string Mark(Player sender, string message, ChatMessageType type)
    {
        if (sender.Attributes?[Stats.IsVip] is not > 0 || type is ChatMessageType.Command or ChatMessageType.GlobalNotification)
        {
            return message;
        }

        var prefix = type switch
        {
            ChatMessageType.Alliance => "@@",
            ChatMessageType.Guild => "@",
            ChatMessageType.Gens => "$",
            _ => string.Empty,
        };
        var text = message.StartsWith(prefix, StringComparison.Ordinal) ? message[prefix.Length..] : message;

        // VIP is the same abbreviation in English and Vietnamese. Keep the sender's name untouched by packet limits.
        return prefix + "[VIP] " + text;
    }
}
