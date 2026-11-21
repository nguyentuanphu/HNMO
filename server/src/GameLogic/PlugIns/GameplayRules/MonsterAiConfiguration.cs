// <copyright file="MonsterAiConfiguration.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Configuration for <see cref="MonsterAiPlugIn"/> (monster chase, leash, and heal-on-leash rules).
/// </summary>
public sealed class MonsterAiConfiguration
{
    /// <summary>Gets or sets a value indicating whether monsters chase attackers from outside their view range.</summary>
    public bool ChaseAttacker { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether monsters leash back to their spawn area when pulled too far.</summary>
    public bool LeashToSpawnArea { get; set; } = true;

    /// <summary>Gets or sets extra tiles beyond <c>MoveRange</c> from the spawn area before a monster leashes home.</summary>
    [Range(0, 30)]
    public int LeashExtraRange { get; set; } = 2;

    /// <summary>Gets or sets a value indicating whether a monster heals to full health when leashing back to its spawn area.</summary>
    public bool HealToFullOnLeash { get; set; } = true;
}
