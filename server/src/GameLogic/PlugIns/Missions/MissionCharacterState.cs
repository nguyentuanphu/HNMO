// <copyright file="MissionCharacterState.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// The mission progress of a character.
/// </summary>
public sealed class MissionCharacterState
{
    /// <summary>Gets or sets the character name (for the logs and the admin panel).</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the login name of its account.</summary>
    public string? Account { get; set; }

    /// <summary>Gets or sets the game day of <see cref="Daily"/>.</summary>
    public string DailyKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the game week (its Monday) of <see cref="Weekly"/>.</summary>
    public string WeeklyKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the daily progress by mission id.</summary>
    public Dictionary<string, MissionProgressState> Daily { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the weekly progress by mission id.</summary>
    public Dictionary<string, MissionProgressState> Weekly { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the days of the streak, counted up to <see cref="StreakLastDay"/>.</summary>
    public int StreakCount { get; set; }

    /// <summary>Gets or sets the last game day which counted for the streak.</summary>
    public string? StreakLastDay { get; set; }

    /// <summary>Gets or sets the game day of a streak bonus which wasn't claimed yet.</summary>
    public string? StreakBonusDay { get; set; }

    /// <summary>Gets or sets the levels whose milestone coins were given.</summary>
    public List<int> MilestonesRewarded { get; set; } = new();

    /// <summary>Gets or sets the resets whose coins were given; -1 until the character was seen.</summary>
    public int ResetsRewarded { get; set; } = -1;
}
