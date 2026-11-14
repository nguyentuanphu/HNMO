// <copyright file="HnmoSkills.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

/// <summary>
/// Knows the HNMO skills (700 to 727, four per class), e.g. for the administration which grants and removes them.
/// </summary>
/// <remarks>
/// The skill number is 700 + 4 * class + letter (A 0, B 1, C 2, D 3); A, B and C are learned by level, D by the
/// trial quest of the class.
/// </remarks>
public static class HnmoSkills
{
    /// <summary>
    /// The number of the first HNMO skill.
    /// </summary>
    public const short FirstSkillNumber = 700;

    /// <summary>
    /// The number of the last HNMO skill.
    /// </summary>
    public const short LastSkillNumber = 727;

    /// <summary>
    /// Determines whether the number is the one of an HNMO skill.
    /// </summary>
    /// <param name="skillNumber">The skill number.</param>
    /// <returns><c>true</c>, if it's an HNMO skill.</returns>
    public static bool IsHnmoSkill(int skillNumber) => skillNumber is >= FirstSkillNumber and <= LastSkillNumber;

    /// <summary>
    /// Gets the letter of an HNMO skill: A, B and C are learned by level, D by the trial quest.
    /// </summary>
    /// <param name="skillNumber">The skill number.</param>
    /// <returns>The letter.</returns>
    public static char GetLetter(int skillNumber) => (char)('A' + ((skillNumber - FirstSkillNumber) % 4));

    /// <summary>
    /// Gets the HNMO skills which the character class can have, ordered by number.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="characterClass">The character class.</param>
    /// <returns>The skills.</returns>
    public static IReadOnlyList<Skill> GetSkillsOf(GameConfiguration configuration, CharacterClass characterClass)
        => configuration.Skills
            .Where(s => IsHnmoSkill(s.Number) && s.QualifiedCharacters.Contains(characterClass))
            .OrderBy(s => s.Number)
            .ToList();

    /// <summary>
    /// Determines whether the character learned the skill (not counting the skills of equipped items).
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="skillNumber">The skill number.</param>
    /// <returns><c>true</c>, if the character learned the skill.</returns>
    public static bool HasLearned(Character character, int skillNumber)
        => character.LearnedSkills.Any(s => s.Skill?.Number == skillNumber);
}
