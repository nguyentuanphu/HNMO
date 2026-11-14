// <copyright file="ChatMute.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using HNMO.Persistence;

/// <summary>
/// Mutes a character's account for a time, with the chat ban of the account (<see cref="Account.ChatBanUntil"/>),
/// like the game master command <c>/chatban</c>. The admin panel's chat log uses it.
/// </summary>
public static class ChatMute
{
    /// <summary>
    /// Mutes the account of a character. A character in the game is muted at once and told so;
    /// otherwise the ban is saved for its account.
    /// </summary>
    /// <param name="persistenceContextProvider">The persistence context provider.</param>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="characterName">The character name.</param>
    /// <param name="duration">The duration.</param>
    /// <returns>The time until the account is muted (UTC); <c>null</c> if the character doesn't exist.</returns>
    public static async ValueTask<DateTime?> MuteAsync(IPersistenceContextProvider persistenceContextProvider, GameConfiguration configuration, string characterName, TimeSpan duration)
    {
        var until = DateTime.UtcNow + duration;
        var minutes = (int)Math.Ceiling(duration.TotalMinutes);
        if (OnlineCharacters.Get(characterName) is { Account: { } account } player)
        {
            // The game saves the account of a player in the game; a change in the database would be overwritten.
            account.ChatBanUntil = until;
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.YouAreChatBanned), minutes).ConfigureAwait(false);
            return until;
        }

        using var context = persistenceContextProvider.CreateNewPlayerContext(configuration);
        if (await context.GetAccountByCharacterNameAsync(characterName).ConfigureAwait(false) is not { } offlineAccount)
        {
            return null;
        }

        offlineAccount.ChatBanUntil = until;
        await context.SaveChangesAsync().ConfigureAwait(false);
        return until;
    }
}
