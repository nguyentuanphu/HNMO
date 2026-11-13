// <copyright file="GuildPlayerLeftGameArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for a notification about a game-leaving guild member.
/// </summary>
public record GuildPlayerLeftGameArguments(uint GuildId, Guid GuildMemberId, byte ServerId);