// <copyright file="S9EventHistoryEntry.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.SeasonNineEvents;

using System;

/// <summary>
/// Recorded Season 9 event execution or winner summary for Admin Panel history.
/// </summary>
public sealed class S9EventHistoryEntry
{
    /// <summary>Gets or sets event key (e.g. <c>acheron</c>, <c>arca</c>, <c>ccs</c>, <c>tsquare</c>, <c>invasion</c>, <c>uruk</c>).</summary>
    public string EventKey { get; set; } = string.Empty;

    /// <summary>Gets or sets human-readable event label.</summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime CompletedAtUtc { get; set; }

    /// <summary>Gets or sets formatted Sydney local timestamp.</summary>
    public string CompletedAtSydney { get; set; } = string.Empty;

    /// <summary>Gets or sets winner or outcome summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets participant or monster count.</summary>
    public int Count { get; set; }
}
