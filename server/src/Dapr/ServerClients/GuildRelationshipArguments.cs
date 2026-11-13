// <copyright file="GuildRelationshipArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for determining the relationship between two guilds.
/// </summary>
public record GuildRelationshipArguments(uint GuildIdA, uint GuildIdB);
