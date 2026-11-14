// <copyright file="HnmoSkillChangeResult.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

/// <summary>
/// The result of granting or removing an HNMO skill.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="Online">Whether the account was online, so the game server changed the live character.</param>
/// <param name="Detail">The message of the game server, when it refused the change.</param>
public record HnmoSkillChangeResult(HnmoSkillChangeStatus Status, bool Online, string? Detail = null);
