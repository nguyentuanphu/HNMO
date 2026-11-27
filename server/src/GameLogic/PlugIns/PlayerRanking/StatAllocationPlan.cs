// <copyright file="StatAllocationPlan.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.PlayerRanking;

/// <summary>
/// Validated multi-stat distribution plan for the in-game Add Stats window.
/// </summary>
/// <param name="Valid">Whether the requested allocation is valid.</param>
/// <param name="ErrorCode">Machine-readable validation error key when <paramref name="Valid"/> is <c>false</c>.</param>
/// <param name="Strength">Points to add to Strength.</param>
/// <param name="Agility">Points to add to Agility.</param>
/// <param name="Vitality">Points to add to Vitality.</param>
/// <param name="Energy">Points to add to Energy.</param>
/// <param name="Command">Points to add to Command (Dark Lord only).</param>
public readonly record struct StatAllocationPlan(
    bool Valid,
    string? ErrorCode,
    int Strength,
    int Agility,
    int Vitality,
    int Energy,
    int Command)
{
    /// <summary>Gets the total stat points across all attributes.</summary>
    public int TotalPoints => this.Strength + this.Agility + this.Vitality + this.Energy + this.Command;
}
