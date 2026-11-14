// <copyright file="HnmoSkillChangeStatus.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>
/// The status of granting or removing an HNMO skill.
/// </summary>
public enum HnmoSkillChangeStatus
{
    /// <summary>
    /// The character learned the skill.
    /// </summary>
    Learned,

    /// <summary>
    /// The skill was removed from the character.
    /// </summary>
    Removed,

    /// <summary>
    /// The character already had (or didn't have) the skill; nothing changed.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The character doesn't exist.
    /// </summary>
    CharacterNotFound,

    /// <summary>
    /// The skill isn't an HNMO skill of the class of the character.
    /// </summary>
    SkillNotOfClass,

    /// <summary>
    /// The account is logging in or reserved by another change; nothing changed.
    /// </summary>
    AccountBusy,

    /// <summary>
    /// The account is online, but its character isn't in the game (e.g. in the character selection); nothing changed.
    /// </summary>
    NotInGame,

    /// <summary>
    /// The account is online, but its game server can't be reached; nothing changed.
    /// </summary>
    ServerUnavailable,
}
