// <copyright file="LorenDeepEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.S6Events;

using System;
using System.Collections.Generic;

/// <summary>Wave spawn entry for Valley of Loren assault (<c>IGC_LorenDeep.xml</c>).</summary>
/// <param name="MonsterNumber">Monster definition number.</param>
/// <param name="Count">Number of monsters to spawn.</param>
public readonly record struct LorenDeepSpawnEntry(short MonsterNumber, ushort Count);

/// <summary>
/// Wave templates and weighted Assault Type selection (Common Assault vs Special Raid)
/// for Valley of Loren (map 30, coordinates 60..132 x 28..101).
/// </summary>
public static class LorenDeepEngine
{
    /// <summary>Assault type identifier.</summary>
    public enum AssaultType
    {
        /// <summary>Type 1: Common Assault.</summary>
        CommonAssault = 1,

        /// <summary>Type 2: Special Raid (includes Axl Hero and Erohim).</summary>
        SpecialRaid = 2,
    }

    /// <summary>Minimum X coordinate of Loren Deep spawn box.</summary>
    public const byte SpawnMinX = 60;

    /// <summary>Maximum X coordinate of Loren Deep spawn box.</summary>
    public const byte SpawnMaxX = 132;

    /// <summary>Minimum Y coordinate of Loren Deep spawn box.</summary>
    public const byte SpawnMinY = 28;

    /// <summary>Maximum Y coordinate of Loren Deep spawn box.</summary>
    public const byte SpawnMaxY = 101;

    /// <summary>Picks Assault Type by configured weights.</summary>
    /// <param name="commonWeight">Weight for Type 1 Common Assault.</param>
    /// <param name="specialWeight">Weight for Type 2 Special Raid.</param>
    /// <param name="randomFraction">Uniform random value in [0, 1).</param>
    /// <returns>Selected <see cref="AssaultType"/>.</returns>
    public static AssaultType SelectAssaultType(int commonWeight, int specialWeight, double randomFraction)
    {
        var w1 = Math.Max(0, commonWeight);
        var w2 = Math.Max(0, specialWeight);
        var total = w1 + w2;
        if (total <= 0)
        {
            return AssaultType.CommonAssault;
        }

        return Math.Clamp(randomFraction, 0.0, 0.999999999) * total < w1
            ? AssaultType.CommonAssault
            : AssaultType.SpecialRaid;
    }

    /// <summary>Gets spawn wave entries for <paramref name="type"/> and 1-based <paramref name="wave"/> (1..3).</summary>
    /// <param name="type">Selected assault type.</param>
    /// <param name="wave">Wave index (1 at +0m, 2 at +10m, 3 at +20m).</param>
    /// <returns>Spawn list for that wave.</returns>
    public static IReadOnlyList<LorenDeepSpawnEntry> GetWaveSpawns(AssaultType type, int wave) => (type, wave) switch
    {
        (AssaultType.CommonAssault, 1) =>
        [
            new(39, 10), new(34, 10), new(41, 10), new(37, 10), new(40, 10), new(35, 10), new(48, 10), new(62, 10), new(300, 1),
        ],
        (AssaultType.CommonAssault, 2) =>
        [
            new(69, 10), new(60, 10), new(71, 10), new(57, 10), new(70, 10), new(58, 10), new(61, 10), new(73, 10), new(74, 10), new(72, 10), new(301, 1),
        ],
        (AssaultType.CommonAssault, 3) =>
        [
            new(49, 8), new(139, 8), new(123, 8), new(59, 8), new(74, 8), new(63, 8), new(293, 12), new(292, 12), new(291, 12), new(303, 1),
        ],
        (AssaultType.SpecialRaid, 1) =>
        [
            new(39, 8), new(34, 8), new(41, 8), new(37, 8), new(40, 8), new(35, 8), new(48, 8), new(62, 8), new(300, 1),
        ],
        (AssaultType.SpecialRaid, 2) =>
        [
            new(294, 8), new(290, 8), new(193, 8), new(194, 8), new(192, 8), new(190, 8), new(195, 8), new(61, 8), new(73, 8), new(127, 8), new(302, 1),
        ],
        (AssaultType.SpecialRaid, 3) =>
        [
            new(49, 8), new(139, 8), new(123, 8), new(59, 8), new(74, 8), new(63, 8), new(293, 12), new(292, 12), new(291, 12), new(303, 1), new(295, 1),
        ],
        _ => [],
    };
}
