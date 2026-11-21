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
    IFeaturePlugIn,
    ISupportCustomConfiguration<MonsterAiConfiguration>,
    ISupportDefaultCustomConfiguration
{
    /// <summary>Gets the rules when the plug-in is switched off: no chase from afar, no leash (the behaviour before it).</summary>
    public static MonsterAiConfiguration Off { get; } = new() { ChaseAttacker = false, LeashToSpawnArea = false, HealToFullOnLeash = false };

    /// <inheritdoc />
    public MonsterAiConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets the rules of a game server. Settings-only plug-ins are found through the feature plug-in point: a plug-in
    /// which isn't a plug-in point's plug-in is never in <see cref="PlugInManager.GetActivePlugInsOf{TPlugIn}"/>.
    /// </summary>
    /// <param name="plugInManager">The game server's plug-in manager.</param>
    /// <returns>The rules; <see cref="Off"/> when the plug-in is switched off.</returns>
    public static MonsterAiConfiguration For(PlugInManager? plugInManager) =>
        plugInManager?.GetActivePlugInsOf<IFeaturePlugIn>().OfType<MonsterAiPlugIn>().FirstOrDefault() is { } plugIn
            ? plugIn.Configuration ?? new MonsterAiConfiguration()
            : Off;

    /// <inheritdoc />
    public object CreateDefaultConfig() => new MonsterAiConfiguration();
}
