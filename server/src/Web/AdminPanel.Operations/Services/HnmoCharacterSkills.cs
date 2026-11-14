// <copyright file="HnmoCharacterSkills.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>
/// A character with the HNMO skills of its class.
/// </summary>
/// <remarks>
/// It's read from the database; while the account is online, the game server saves changes of the live character
/// from time to time, and at once when the admin panel changes the skills.
/// </remarks>
/// <param name="CharacterName">The name of the character.</param>
/// <param name="ClassName">The name of the character class.</param>
/// <param name="Level">The level of the character.</param>
/// <param name="Online">Whether the account is online.</param>
/// <param name="Skills">The HNMO skills of the class, ordered by number.</param>
public record HnmoCharacterSkills(string CharacterName, string ClassName, int Level, bool Online, IReadOnlyList<HnmoSkillState> Skills);
