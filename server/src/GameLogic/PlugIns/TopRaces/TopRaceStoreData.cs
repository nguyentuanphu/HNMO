// <copyright file="TopRaceStoreData.cs" company="HNMO">
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
/// The saved state of the top races, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class TopRaceStoreData
{
    /// <summary>Gets or sets the running races by "race id|period key".</summary>
    public Dictionary<string, TopRaceRun> Runs { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the last level and resets seen, by character id.</summary>
    public Dictionary<string, TopRaceSeen> Seen { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the finished races, newest first.</summary>
    public List<TopRaceResult> History { get; set; } = new();

    /// <summary>Gets or sets the prizes which wait for room in the bag, by character id.</summary>
    public Dictionary<string, List<TopRaceOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);
}
