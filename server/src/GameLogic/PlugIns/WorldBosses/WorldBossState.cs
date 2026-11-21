// <copyright file="WorldBossState.cs" company="HNMO">
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
/// The schedule state of a world boss.
/// </summary>
public sealed class WorldBossState
{
    /// <summary>Gets or sets the next spawn time.</summary>
    public DateTime? NextSpawnUtc { get; set; }

    /// <summary>Gets or sets the spawn time which was announced before.</summary>
    public DateTime? AnnouncedUtc { get; set; }

    /// <summary>Gets or sets the schedule (as text) <see cref="NextSpawnUtc"/> was computed from, so an edit recomputes it.</summary>
    public string? ScheduleKey { get; set; }
}
