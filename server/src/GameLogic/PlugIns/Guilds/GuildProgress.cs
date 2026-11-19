// <copyright file="GuildProgress.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// The experience and level of a guild.
/// </summary>
public sealed class GuildProgress
{
    /// <summary>Gets or sets the guild's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the guild experience in total.</summary>
    public long Xp { get; set; }

    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Gets or sets the ladder points of the current week (its guild experience plus bonuses).</summary>
    public long WeekXp { get; set; }

    /// <summary>Gets or sets when the week's points were last raised (an earlier time wins a tie).</summary>
    public DateTime WeekUpdatedAtUtc { get; set; }

    /// <summary>Gets or sets the week's guild experience by member name.</summary>
    public Dictionary<string, long> WeekMembers { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets when the guild reached its level.</summary>
    public DateTime? LevelUpAtUtc { get; set; }
}
