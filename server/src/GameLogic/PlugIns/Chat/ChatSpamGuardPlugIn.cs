// <copyright file="ChatSpamGuardPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Drops the chat lines of a player who sends the same text too often, or too many lines in a short time,
/// and tells the player "Please don't spam.". Game masters aren't checked.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ChatSpamGuardPlugIn_Name), Description = nameof(PlugInResources.ChatSpamGuardPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("A6F03D2B-91C4-4E7A-B85F-0C3E7D14A962")]
public class ChatSpamGuardPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<ChatSpamGuardConfiguration>, ISupportDefaultCustomConfiguration
{
    private readonly ConditionalWeakTable<Player, ChatSpamGuard> _guards = new();

    /// <inheritdoc />
    public ChatSpamGuardConfiguration? Configuration { get; set; }

    /// <summary>
    /// Checks a chat line of a player with the active guard of its game; a line which is spam is dropped
    /// and the player is told so.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="message">The message.</param>
    /// <returns><c>true</c>, if the line may be sent.</returns>
    public static async ValueTask<bool> CheckAsync(Player sender, string message)
    {
        if (sender.GameContext.FeaturePlugIns.GetPlugIn<ChatSpamGuardPlugIn>() is not { } guard
            || guard.TryAccept(sender, message, DateTime.UtcNow))
        {
            return true;
        }

        await sender.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.ChatSpamWarning)).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Checks a chat line of a player.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="message">The message.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><c>true</c>, if the line may be sent.</returns>
    public bool TryAccept(Player sender, string message, DateTime nowUtc)
    {
        if (sender.SelectedCharacter?.CharacterStatus is CharacterStatus.GameMaster)
        {
            return true;
        }

        var limits = (this.Configuration ?? new ChatSpamGuardConfiguration()).ToLimits();
        return this._guards.GetValue(sender, _ => new ChatSpamGuard()).TryAccept(message, nowUtc, limits);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new ChatSpamGuardConfiguration();
    }
}
