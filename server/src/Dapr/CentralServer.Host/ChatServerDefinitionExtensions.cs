// <copyright file="ChatServerDefinitionExtensions.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.CentralServer.Host;

using HNMO.ChatServer;
using HNMO.DataModel.Configuration;
using HNMO.Interfaces;
using HNMO.Network.PlugIns;
using HNMO.Persistence;

/// <summary>
/// Extensions for <see cref="ChatServerDefinition"/>.
/// </summary>
public static class ChatServerDefinitionExtensions
{
    /// <summary>
    /// Converts the <see cref="ChatServerDefinition"/> into corresponding <see cref="ChatServerSettings"/>.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The settings.</returns>
    public static ChatServerSettings ConvertToSettings(this ChatServerDefinition definition)
    {
        var result = new ChatServerSettings
        {
            Id = definition.GetId(),
            MaximumConnections = definition.MaximumConnections,
            ClientTimeout = definition.ClientTimeout,
            ClientCleanUpInterval = definition.ClientCleanUpInterval,
            RoomCleanUpInterval = definition.RoomCleanUpInterval,
            Description = definition.Description,
            ServerId = definition.ServerId + SpecialServerIds.ChatServer,
        };
        foreach (var endpoint in definition.Endpoints)
        {
            result.Endpoints.Add(new HNMO.ChatServer.ChatServerEndpoint
            {
                ClientVersion = new ClientVersion(endpoint.Client!.Season, endpoint.Client.Episode, endpoint.Client.Language),
                NetworkPort = endpoint.NetworkPort,
            });
        }

        return result;
    }
}