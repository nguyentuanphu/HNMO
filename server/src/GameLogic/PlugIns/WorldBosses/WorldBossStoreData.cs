// <copyright file="WorldBossStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// The saved state of the world bosses and the weekly rankings, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class WorldBossStoreData
{
    /// <summary>Gets or sets the schedule state by boss id.</summary>
    public Dictionary<string, WorldBossState> Bosses { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the bosses which are on a map now.</summary>
    public List<WorldBossAliveState> Alive { get; set; } = new();

    /// <summary>Gets or sets the recent kills and escapes, newest first.</summary>
    public List<WorldBossKillRecord> Kills { get; set; } = new();

    /// <summary>Gets or sets the recent announcements, newest first.</summary>
    public List<WorldBossAnnouncement> Announcements { get; set; } = new();

    /// <summary>Gets or sets the rewards which wait to be given, by character id.</summary>
    public Dictionary<string, List<WorldBossOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the current week (its Monday).</summary>
    public string WeekKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the points of the current week: by category, then by character id.</summary>
    public Dictionary<WeeklyCategory, Dictionary<string, WeeklyRankingEntry>> Week { get; set; } = new();

    /// <summary>Gets or sets the persistent id of the guild which won the castle siege in the current week.</summary>
    public Guid? CastleSiegeGuildId { get; set; }

    /// <summary>Gets or sets the name of that guild.</summary>
    public string? CastleSiegeGuildName { get; set; }

    /// <summary>Gets or sets the result of the last finished week.</summary>
    public WeeklyResult? LastWeek { get; set; }

    /// <summary>Gets or sets the finished weeks which wait to be paid.</summary>
    public List<WeeklyResult> PendingWeeks { get; set; } = new();

    /// <summary>Gets or sets the weeks which were paid.</summary>
    public List<string> PaidWeeks { get; set; } = new();
}
