// <copyright file="TimetableStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Timetable;

using System.IO;
using System.Text.Json;

/// <summary>
/// Persisted section stored under <c>timetable</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class TimetableStoreData
{
    /// <summary>Gets or sets recent Find the GM winners, newest first.</summary>
    public List<FindGmWinnerEntry> FindGmWinners { get; set; } = [];
}
