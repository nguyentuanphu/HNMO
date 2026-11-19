// <copyright file="GuildLadderEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Globalization;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A guild's place in this week's ladder.
/// </summary>
/// <param name="Rank">The rank, from 1.</param>
/// <param name="GuildId">The persistent guild id.</param>
/// <param name="Progress">The guild's progress.</param>
public sealed record GuildLadderEntry(int Rank, Guid GuildId, GuildProgress Progress);
