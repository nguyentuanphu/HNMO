// <copyright file="ArenaPlayerRecord.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

/// <summary>An account's ranked 1v1 record in the HNMO Arena.</summary>
public sealed class ArenaPlayerRecord
{
    /// <summary>Gets or sets the account's login name.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the arena character it played last.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the class it played last.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Gets or sets the rating (starts at 1000).</summary>
    public int Rating { get; set; } = ArenaRanking.StartRating;

    /// <summary>Gets or sets the duels won in all.</summary>
    public int Wins { get; set; }

    /// <summary>Gets or sets the duels lost in all.</summary>
    public int Losses { get; set; }

    /// <summary>Gets or sets the Sydney week (e.g. "2026-W47") the week's counts are of.</summary>
    public string WeekKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the duels won this week.</summary>
    public int WeekWins { get; set; }

    /// <summary>Gets or sets the duels lost this week.</summary>
    public int WeekLosses { get; set; }

    /// <summary>Gets or sets when it last played (UTC).</summary>
    public DateTime LastMatchUtc { get; set; }
}
