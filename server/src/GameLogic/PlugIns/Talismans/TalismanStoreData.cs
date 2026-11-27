// <copyright file="TalismanStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

/// <summary>
/// Root store container for <see cref="TalismanStore"/>.
/// </summary>
public sealed class TalismanStoreData
{
    /// <summary>Gets or sets per-character stat-consumable records keyed by character ID.</summary>
    public Dictionary<string, TalismanCharacterRecord> Characters { get; set; } = new(StringComparer.Ordinal);
}
