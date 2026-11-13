// <copyright file="GuildHostilityChangedArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for the notification that a hostility between two guilds was created or removed.
/// </summary>
public record GuildHostilityChangedArguments(uint GuildIdA, IReadOnlyList<uint> AllianceGuildIdsA, uint GuildIdB, IReadOnlyList<uint> AllianceGuildIdsB, bool Created);
