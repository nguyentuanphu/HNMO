// <copyright file="ArenaGuildRecord.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

/// <summary>A guild's guild war record in the Arena: all time and in the current Sydney week.</summary>
public sealed class ArenaGuildRecord
{
    /// <summary>Gets or sets the guild's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the guild's id on Season 6.3 (for its guild experience).</summary>
    public Guid GuildId { get; set; }

    /// <summary>Gets or sets the wins of all time.</summary>
    public int Wins { get; set; }

    /// <summary>Gets or sets the losses of all time.</summary>
    public int Losses { get; set; }

    /// <summary>Gets or sets the draws of all time.</summary>
    public int Draws { get; set; }

    /// <summary>Gets or sets the Sydney week the week counts are for (e.g. "2026-W47").</summary>
    public string Week { get; set; } = string.Empty;

    /// <summary>Gets or sets the wins of that week.</summary>
    public int WeekWins { get; set; }

    /// <summary>Gets or sets the losses of that week.</summary>
    public int WeekLosses { get; set; }
}
