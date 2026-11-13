// <copyright file="IGameEventListener.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

/// <summary>
/// A listener for <see cref="GameEvent"/>s, which runs in the same process as the publishing game servers.
/// </summary>
/// <remarks>
/// When the game servers run in separate processes, the events are published as messages instead,
/// with the topic name <c>GameEventAsync</c>.
/// </remarks>
public interface IGameEventListener
{
    /// <summary>
    /// Is called when a game event has been published.
    /// </summary>
    /// <param name="gameEvent">The game event.</param>
    ValueTask OnGameEventAsync(GameEvent gameEvent);
}
