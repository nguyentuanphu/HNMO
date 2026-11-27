// <copyright file="TopRaceResult.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// A finished race.
/// </summary>
public sealed class TopRaceResult
{
    /// <summary>Gets or sets the race's id.</summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English title.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets what it counted.</summary>
    public TopRaceKind Kind { get; set; }

    /// <summary>Gets or sets the period.</summary>
    public TopRacePeriod Period { get; set; }

    /// <summary>Gets or sets the period's key.</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the start.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Gets or sets the end.</summary>
    public DateTime EndUtc { get; set; }

    /// <summary>Gets or sets the tables.</summary>
    public List<TopRaceTable> Tables { get; set; } = new();

    /// <summary>Gets or sets the references which were paid already (so a retry pays nobody twice).</summary>
    public List<string> PaidReferences { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether all prizes were paid.</summary>
    public bool Paid { get; set; }

    /// <summary>Gets or sets a value indicating whether its end was announced.</summary>
    public bool EndAnnounced { get; set; }
}
