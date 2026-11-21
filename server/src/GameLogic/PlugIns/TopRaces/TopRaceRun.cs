// <copyright file="TopRaceRun.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A race which runs now (one period of it).
/// </summary>
public sealed class TopRaceRun
{
    /// <summary>Gets or sets the race's id.</summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the period's key, e.g. "w2026-11-09".</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the start.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Gets or sets the end.</summary>
    public DateTime EndUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether its start was announced.</summary>
    public bool StartAnnounced { get; set; }

    /// <summary>Gets or sets the entries by key.</summary>
    public Dictionary<string, TopRaceEntry> Entries { get; set; } = new(StringComparer.Ordinal);
}
