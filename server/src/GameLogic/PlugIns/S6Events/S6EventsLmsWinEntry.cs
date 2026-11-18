// <copyright file="S6EventsLmsWinEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;

/// <summary>
/// Recorded winner summary for a completed Last Man Standing round.
/// </summary>
public sealed class S6EventsLmsWinEntry
{
    /// <summary>Gets or sets UTC completion timestamp.</summary>
    public DateTime CompletedAtUtc { get; set; }

    /// <summary>Gets or sets Sydney local completion timestamp string.</summary>
    public string CompletedAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets winner account login name.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets winner character name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets total participants in the round.</summary>
    public int Participants { get; set; }

    /// <summary>Gets or sets kills scored during the round.</summary>
    public int Kills { get; set; }

    /// <summary>Gets or sets summary of rewards granted.</summary>
    public string RewardSummary { get; set; } = string.Empty;
}
