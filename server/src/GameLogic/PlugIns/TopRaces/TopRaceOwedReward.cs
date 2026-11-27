// <copyright file="TopRaceOwedReward.cs" company="HNMO">
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
/// A prize which waits for room in the bag (zen and items; the coins are given at once).
/// </summary>
public sealed class TopRaceOwedReward
{
    /// <summary>Gets or sets the reference; it makes the prize count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the race's id.</summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English title.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the period's key.</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the prize (zen and items).</summary>
    public MissionReward Reward { get; set; } = new();
}
