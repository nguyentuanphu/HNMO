// <copyright file="TopRaceTable.cs" company="HNMO">
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
/// A table of a finished race: all classes, or one class.
/// </summary>
public sealed class TopRaceTable
{
    /// <summary>Gets or sets the class of the table; -1 for all classes.</summary>
    public int Class { get; set; } = -1;

    /// <summary>Gets or sets the lines, best first.</summary>
    public List<TopRaceLine> Lines { get; set; } = new();
}
