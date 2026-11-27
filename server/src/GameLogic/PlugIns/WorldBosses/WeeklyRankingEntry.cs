// <copyright file="WeeklyRankingEntry.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// The points of a character in a weekly ranking.
/// </summary>
public sealed class WeeklyRankingEntry
{
    /// <summary>Gets or sets the character name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the login name of the account.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the points.</summary>
    public long Points { get; set; }

    /// <summary>Gets or sets when the points were last raised (an earlier time wins a tie).</summary>
    public DateTime UpdatedAtUtc { get; set; }
}
