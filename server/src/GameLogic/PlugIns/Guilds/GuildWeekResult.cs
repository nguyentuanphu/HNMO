// <copyright file="GuildWeekResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A finished week of the guild ladder.
/// </summary>
public sealed class GuildWeekResult
{
    /// <summary>Gets or sets the week (its Monday).</summary>
    public string Week { get; set; } = string.Empty;

    /// <summary>Gets or sets the ladder, best first (the top 10).</summary>
    public List<GuildLadderLine> Lines { get; set; } = new();

    /// <summary>Gets or sets the references which were paid already (so a retry pays nobody twice).</summary>
    public List<string> PaidReferences { get; set; } = new();
}
