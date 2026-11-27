// <copyright file="WeeklyResult.cs" company="HNMO">
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
/// The results of a finished week.
/// </summary>
public sealed class WeeklyResult
{
    /// <summary>Gets or sets the week (its Monday).</summary>
    public string Week { get; set; } = string.Empty;

    /// <summary>Gets or sets the winners by category.</summary>
    public Dictionary<WeeklyCategory, List<WeeklyWinner>> Winners { get; set; } = new();

    /// <summary>Gets or sets the guild which won the castle siege that week, if any.</summary>
    public string? CastleSiegeGuild { get; set; }

    /// <summary>Gets or sets the persistent id of that guild.</summary>
    public Guid? CastleSiegeGuildId { get; set; }

    /// <summary>Gets or sets the members of that guild who were paid, with their coins.</summary>
    public List<WeeklyWinner> CastleSiegeMembers { get; set; } = new();
}
