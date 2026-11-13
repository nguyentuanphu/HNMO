// <copyright file="GuildMemberCreationArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

using HNMO.Interfaces;

/// <summary>
/// Arguments for a guild member creation.
/// </summary>
public record GuildMemberCreationArguments(uint GuildId, Guid CharacterId, string CharacterName, GuildPosition Role, byte ServerId);