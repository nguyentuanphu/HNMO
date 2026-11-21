// <copyright file="InMemoryEventPublisher.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Startup;

using HNMO.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// An <see cref="IEventPublisher"/> which publishes directly to the available started servers in this application.
/// </summary>
public class InMemoryEventPublisher : IEventPublisher
{
    private readonly IDictionary<int, IGameServer> _gameServers;
    private readonly IFriendServer _friendServer;
    private readonly IGuildServer _guildServer;
    private readonly IReadOnlyCollection<IGameEventListener> _gameEventListeners;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryEventPublisher"/> class.
    /// </summary>
    /// <param name="gameServers">The game servers.</param>
    /// <param name="friendServer">The friend server.</param>
    /// <param name="guildServer">The guild server.</param>
    /// <param name="gameEventListeners">The listeners for the <see cref="GameEvent"/>s.</param>
    /// <param name="logger">The logger.</param>
    public InMemoryEventPublisher(IDictionary<int, IGameServer> gameServers, IFriendServer friendServer, IGuildServer guildServer, IEnumerable<IGameEventListener> gameEventListeners, ILogger<InMemoryEventPublisher> logger)
    {
        this._gameServers = gameServers;
        this._friendServer = friendServer;
        this._guildServer = guildServer;
        this._gameEventListeners = gameEventListeners.ToList();
        this._logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask PlayerEnteredGameAsync(byte serverId, Guid characterId, string characterName)
    {
        // Each server on its own, as the central server does (EventController): an error of the guild server
        // left the friend server without the player, so its friends saw it offline while it played.
        try
        {
            await this._guildServer.PlayerEnteredGameAsync(characterId, characterName, serverId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error when notifying the guild server that player {CharacterName} entered the game.", characterName);
        }

        await this._friendServer.PlayerEnteredGameAsync(serverId, characterId, characterName).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PlayerLeftGameAsync(byte serverId, Guid characterId, string characterName, uint guildId = 0)
    {
        if (guildId > 0)
        {
            try
            {
                await this._guildServer.GuildMemberLeftGameAsync(guildId, characterId, serverId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error when notifying the guild server that player {CharacterName} left the game.", characterName);
            }
        }

        await this._friendServer.PlayerLeftGameAsync(characterId, characterName).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask GuildMessageAsync(uint guildId, string sender, string message)
    {
        foreach (var gameServer in this._gameServers)
        {
            await gameServer.Value.GuildChatMessageAsync(guildId, sender, message).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask AllianceMessageAsync(uint guildId, string sender, string message)
    {
        foreach (var gameServer in this._gameServers)
        {
            await gameServer.Value.AllianceChatMessageAsync(guildId, sender, message).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask WorldChatMessageAsync(string sender, string message)
    {
        foreach (var gameServer in this._gameServers)
        {
            await gameServer.Value.WorldChatMessageAsync(sender, message).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask PlayerAlreadyLoggedInAsync(byte serverId, string loginName)
    {
        foreach (var gameServer in this._gameServers)
        {
            await gameServer.Value.PlayerAlreadyLoggedInAsync(serverId, loginName).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask GameEventAsync(GameEvent gameEvent)
    {
        foreach (var listener in this._gameEventListeners)
        {
            try
            {
                await listener.OnGameEventAsync(gameEvent).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error when notifying the listener {listener} about the game event {gameEvent}", listener, gameEvent);
            }
        }
    }
}