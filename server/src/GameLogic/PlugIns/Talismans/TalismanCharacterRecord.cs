// <copyright file="TalismanCharacterRecord.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

/// <summary>
/// Per-character persistent record tracking stat points claimed via stat-point consumables.
/// </summary>
public sealed class TalismanCharacterRecord
{
    /// <summary>Gets or sets the cumulative extra stat points granted across all stat consumables.</summary>
    public int TotalExtraStatPoints { get; set; }

    /// <summary>Gets or sets claimed points broken down by bucket (<c>levelUpPoints</c>, <c>str</c>, etc.).</summary>
    public Dictionary<string, int> ByBucket { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the UTC timestamp of the last claim.</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
