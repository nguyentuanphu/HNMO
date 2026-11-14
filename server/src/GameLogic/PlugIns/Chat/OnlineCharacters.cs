// <copyright file="OnlineCharacters.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Chat;

using System.Collections.Concurrent;

/// <summary>
/// The characters which are in the game, over all game servers of this process. A whisper reaches
/// a character on another game server of the same process through it, and the admin panel's chat log
/// mutes the senders through it.
/// </summary>
public static class OnlineCharacters
{
    private static readonly ConcurrentDictionary<string, Player> Players = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the player of a character which is in the game.
    /// </summary>
    /// <param name="characterName">The character name.</param>
    /// <returns>The player; <c>null</c> if the character isn't in the game.</returns>
    public static Player? Get(string characterName)
    {
        return Players.TryGetValue(characterName, out var player) ? player : null;
    }

    /// <summary>
    /// Registers a character which entered the game.
    /// </summary>
    /// <param name="characterName">The character name.</param>
    /// <param name="player">The player.</param>
    internal static void Add(string characterName, Player player)
    {
        Players[characterName] = player;
    }

    /// <summary>
    /// Removes a character which left the game; nothing happens if the name belongs to another player by now.
    /// </summary>
    /// <param name="characterName">The character name.</param>
    /// <param name="player">The player.</param>
    internal static void Remove(string characterName, Player player)
    {
        ((ICollection<KeyValuePair<string, Player>>)Players).Remove(new(characterName, player));
    }
}
