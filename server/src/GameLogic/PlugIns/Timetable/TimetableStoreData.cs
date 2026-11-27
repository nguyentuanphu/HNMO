// <copyright file="TimetableStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
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
