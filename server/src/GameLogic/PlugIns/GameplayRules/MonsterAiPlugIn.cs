// <copyright file="MonsterAiPlugIn.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.GameplayRules;

using System.Runtime.InteropServices;
using HNMO.PlugIns;

/// <summary>
/// Holds monster chase, leash, and heal-on-leash configuration for the game server and Admin Panel.
/// </summary>
[PlugIn]
[Display(Name = "Monster AI Rules", Description = "Configures monster attacker chasing, spawn-area leashing, and heal-on-leash behavior.")]
[Guid("8F3B2A10-5C4E-4D92-B7A1-2E6F8C9D0315")]
public sealed class MonsterAiPlugIn :
    ISupportCustomConfiguration<MonsterAiConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public MonsterAiConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MonsterAiConfiguration();
}
