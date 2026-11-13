// <copyright file="GuildMemberAssignArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

using HNMO.Interfaces;

/// <summary>
/// Arguments for a guild member assignment.
/// </summary>
public record GuildMemberAssignArguments(string CharacterName, GuildMemberStatus MemberStatus);