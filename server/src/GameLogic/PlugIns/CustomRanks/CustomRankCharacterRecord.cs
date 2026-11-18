// <copyright file="CustomRankCharacterRecord.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CustomRanks;

/// <summary>
/// Persisted per-character custom rank preference and granted achievement keys.
/// </summary>
public sealed class CustomRankCharacterRecord
{
    /// <summary>Gets or sets the character name.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the chosen rank tier id, or empty string for automatic highest-priority rank.</summary>
    public string SelectedRankId { get; set; } = string.Empty;

    /// <summary>Gets or sets special achievement keys granted to this character.</summary>
    public List<string> AchievementKeys { get; set; } = [];
}
