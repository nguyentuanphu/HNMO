// <copyright file="CustomRankStoreData.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CustomRanks;

/// <summary>
/// Serialized store payload for custom ranks and granted achievement keys.
/// </summary>
public sealed class CustomRankStoreData
{
    /// <summary>Gets or sets persisted character rank selections and achievement keys keyed by lowercase character name.</summary>
    public Dictionary<string, CustomRankCharacterRecord> Characters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
