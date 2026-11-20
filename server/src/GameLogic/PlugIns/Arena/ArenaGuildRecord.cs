// <copyright file="ArenaGuildRecord.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Arena;

/// <summary>A guild's guild war record in the Arena: all time and in the current Sydney week.</summary>
public sealed class ArenaGuildRecord
{
    /// <summary>Gets or sets the guild's name.</summary>
    public string Name { get; set; } = string.Empty;

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
