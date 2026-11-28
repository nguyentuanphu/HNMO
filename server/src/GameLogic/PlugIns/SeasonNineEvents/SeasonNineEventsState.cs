// <copyright file="SeasonNineEventsState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineEvents;

using System;
using System.Collections.Generic;

/// <summary>
/// Persisted state payload stored in <see cref="HnmoStateStore"/> (<c>s9Events</c>).
/// </summary>
public sealed class SeasonNineEventsState
{
    /// <summary>Gets or sets last claimed schedule slot (<c>yyyy-MM-dd HH:mm</c>) keyed by event or invasion id.</summary>
    public Dictionary<string, string> LastSlotByEvent { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets pending item/Zen rewards awaiting bag room or login.</summary>
    public List<S9EventOwedReward> OwedRewards { get; set; } = [];

    /// <summary>Gets or sets recent Season 9 event log entries (newest first).</summary>
    public List<S9EventHistoryEntry> History { get; set; } = [];

    /// <summary>Gets or sets last UTC timestamp when Uruk Mountain boss guards spawned.</summary>
    public DateTime? LastUrukGuardSpawnUtc { get; set; }
}
