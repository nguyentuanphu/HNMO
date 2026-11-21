// <copyright file="GuildKey.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Text.Json;
using System.Text.Json.Serialization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// The guild of a player, as the plug-in knows it.
/// </summary>
/// <param name="GuildId">The persistent guild id.</param>
/// <param name="Name">The guild's name.</param>
public sealed record GuildKey(Guid GuildId, string Name);
