// <copyright file="GuildStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// The saved state of the guild levels and the ladder, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class GuildStoreData
{
    /// <summary>Gets or sets the progress by persistent guild id.</summary>
    public Dictionary<Guid, GuildProgress> Guilds { get; set; } = new();

    /// <summary>Gets or sets the current day.</summary>
    public string DayKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the capped guild experience earned today, by character id.</summary>
    public Dictionary<string, int> Today { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the current week (its Monday).</summary>
    public string WeekKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the last finished week.</summary>
    public GuildWeekResult? LastWeek { get; set; }

    /// <summary>Gets or sets the finished weeks which wait to be paid.</summary>
    public List<GuildWeekResult> PendingWeeks { get; set; } = new();

    /// <summary>Gets or sets the weeks which were paid.</summary>
    public List<string> PaidWeeks { get; set; } = new();

    /// <summary>Gets or sets the castle sieges, newest first.</summary>
    public List<GuildSiegeResult> Sieges { get; set; } = new();

    /// <summary>Gets or sets the rewards which wait for room in the bag, by character id.</summary>
    public Dictionary<string, List<GuildOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);
}
