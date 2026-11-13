// <copyright file="GuildHostilityArguments.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.ServerClients;

/// <summary>
/// Arguments for creating or removing a hostility between two guilds.
/// </summary>
public record GuildHostilityArguments(uint GuildIdA, uint GuildIdB, bool Create);
