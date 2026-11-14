// <copyright file="HnmoSkillState.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>
/// An HNMO skill of a character class, and whether a character learned it.
/// </summary>
/// <param name="Number">The skill number (700 to 727).</param>
/// <param name="Letter">The letter: A, B and C are learned by level, D by the trial quest of the class.</param>
/// <param name="Name">The name of the skill.</param>
/// <param name="RequiredLevel">The level which the skill requires; for the D skill, the level from which its trial quest can be done.</param>
/// <param name="Learned">Whether the character learned the skill.</param>
public record HnmoSkillState(int Number, char Letter, string Name, int RequiredLevel, bool Learned);
