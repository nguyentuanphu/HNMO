// <copyright file="CustomRankStoreData.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
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
