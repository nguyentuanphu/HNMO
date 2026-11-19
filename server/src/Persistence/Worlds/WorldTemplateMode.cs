// <copyright file="WorldTemplateMode.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Persistence.Worlds;

/// <summary>
/// Specifies how character and account data are handled when creating a new world from a template.
/// </summary>
public enum WorldTemplateMode
{
    /// <summary>
    /// Copies configuration, rates, and event rules from the template world with an empty character roster.
    /// </summary>
    Fresh = 0,

    /// <summary>
    /// Copies configuration, rates, event rules, and eligible accounts/characters from the template world.
    /// </summary>
    CopiedCharacters = 1,
}
