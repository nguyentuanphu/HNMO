// <copyright file="S6EventsState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.Collections.Generic;

/// <summary>
/// Persisted state payload stored in <see cref="HnmoStateStore"/> (<c>s6Events</c>).
/// </summary>
public sealed class S6EventsState
{
    /// <summary>Gets or sets last LMS schedule key triggered (<c>yyyy-MM-dd HH:mm</c>).</summary>
    public string LastTriggeredSlotKey { get; set; } = string.Empty;

    /// <summary>Gets or sets most recent LMS finish UTC timestamp.</summary>
    public DateTime? LastLmsCompletedUtc { get; set; }

    /// <summary>Gets or sets pending item/Zen rewards awaiting bag space or next login.</summary>
    public List<S6EventsOwedReward> OwedRewards { get; set; } = [];

    /// <summary>Gets or sets recent Last Man Standing winner history (newest first).</summary>
    public List<S6EventsLmsWinEntry> LmsHistory { get; set; } = [];

    /// <summary>Gets or sets last Loren Deep schedule key triggered (<c>yyyy-MM-dd HH:mm</c>).</summary>
    public string LastLorenTriggeredSlotKey { get; set; } = string.Empty;

    /// <summary>Gets or sets per-character UTC timestamps of last Snowman (467) / <c>/xmas</c> gift claim.</summary>
    public Dictionary<string, DateTime> LastXMasGiftUtcByCharacter { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets active Mu Rummy sessions keyed by character name.</summary>
    public Dictionary<string, MuRummyGameState> MuRummyGames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
