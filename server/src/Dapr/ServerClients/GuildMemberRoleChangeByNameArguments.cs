// <copyright file="GuildMemberRoleChangeByNameArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

using HNMO.Interfaces;

/// <summary>
/// Arguments for a guild member role change by character name.
/// </summary>
public record GuildMemberRoleChangeByNameArguments(uint GuildId, string CharacterName, GuildPosition NewRole, int MasterTotalLevel);
