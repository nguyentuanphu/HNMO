// <copyright file="SkillDelays.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using System.Collections.Frozen;

/// <summary>
/// The skills' delays (cooldowns) which the server checks, as in classic MU (CheckSkillDelay): a skill with a delay can't
/// be used again before its delay has passed. The values are the delays of the Season 6/9 server suites; skills
/// without an entry have no delay. Chain Drive (and its master skills) is left out: the client sends two hits for one
/// cast (at two keys of its action), so a 700 ms delay would refuse its second hit.
/// </summary>
public static class SkillDelays
{
    /// <summary>
    /// The part of a delay which has to pass before the skill may be used again. It is a bit less than the full delay,
    /// so that a cast which reaches the server earlier than the previous one (network jitter) isn't refused.
    /// </summary>
    public const double ToleranceFactor = 0.8;

    private static readonly FrozenDictionary<short, int> DelaysInMilliseconds = new Dictionary<short, int>
    {
        [62] = 10_000, // Earthshake
        [63] = 30_000, // Summon
        [65] = 5_000, // Electric Spike
        [76] = 1_500, // Plasma Storm
        [217] = 5_000, // Damage Reflection
        [218] = 5_000, // Berserker
        [221] = 4_000, // Weakness
        [222] = 4_000, // Innovation
        [223] = 4_000, // Explosion
        [224] = 4_000, // Requiem
        [225] = 4_000, // Pollution
        [264] = 700, // Dragon Roar
        [265] = 3_000, // Dragon Slasher
        [270] = 1_200, // Phoenix Shot
        [344] = 10_000, // Blood Storm
        [346] = 10_000, // Blood Storm Strengthener
        [425] = 3_000, // Cure
        [426] = 10_000, // Party Healing
        [427] = 10_000, // Poison Arrow
        [429] = 10_000, // Party Healing Strengthener
        [430] = 3_000, // Bless
        [433] = 3_000, // Bless Strengthener
        [434] = 10_000, // Poison Arrow Strengthener
        [441] = 200, // Infinity Arrow Strengthener
        [459] = 4_000, // Weakness Strengthener
        [460] = 4_000, // Innovation Strengthener
        [461] = 10_000, // Blind
        [463] = 10_000, // Blind Strengthener
        [469] = 5_000, // Berserker Strengthener
        [470] = 5_000, // Berserker Proficiency
        [472] = 5_000, // Berserker Mastery
        [495] = 10_000, // Earth Prison
        [497] = 10_000, // Earth Prison Strengthener
        [512] = 10_000, // Earthshake Strengthener
        [516] = 10_000, // Earthshake Mastery
        [519] = 5_000, // Electric Spark Strengthener
        [521] = 60_000, // Iron Defense
        [524] = 60_000, // Iron Defense Strengthener
        [560] = 700, // Dragon Roar Strengthener
        [561] = 700, // Dragon Roar Mastery
        [564] = 3_000, // Dragon Slasher Strengthener
        [565] = 120_000, // Blood Howling
        [566] = 3_000, // Dragon Slasher Mastery
        [567] = 120_000, // Blood Howling Strengthener
        [618] = 60_000, // Spell of Protection
        [619] = 60_000, // Spell of Restriction
        [620] = 120_000, // Spell of Pursuit
        [621] = 60_000, // Shield-Burn
    }.ToFrozenDictionary();

    /// <summary>
    /// Gets the delay of a skill.
    /// </summary>
    /// <param name="skillNumber">The skill number.</param>
    /// <returns>The delay; <see cref="TimeSpan.Zero"/> for a skill without delay.</returns>
    public static TimeSpan GetDelay(short skillNumber)
        => DelaysInMilliseconds.TryGetValue(skillNumber, out var delay) ? TimeSpan.FromMilliseconds(delay) : TimeSpan.Zero;

    /// <summary>
    /// Determines whether a skill which was last used at <paramref name="lastUse"/> may be used at <paramref name="now"/>.
    /// </summary>
    /// <param name="skillNumber">The skill number.</param>
    /// <param name="lastUse">The time of the last use, if any.</param>
    /// <param name="now">The current time.</param>
    /// <returns><c>true</c> if the skill may be used.</returns>
    public static bool HasPassed(short skillNumber, DateTime? lastUse, DateTime now)
    {
        var delay = GetDelay(skillNumber);
        return lastUse is null || delay <= TimeSpan.Zero || now - lastUse.Value >= delay * ToleranceFactor;
    }
}
