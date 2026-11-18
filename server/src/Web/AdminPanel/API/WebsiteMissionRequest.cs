// <copyright file="WebsiteMissionRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

/// <summary>Credentials and an owned character, plus an optional explicit mission action.</summary>
/// <param name="LoginName">The account name.</param>
/// <param name="Password">Its current password.</param>
/// <param name="CharacterName">The character selected from the authenticated account.</param>
/// <param name="Action">The explicit claim/invitation action.</param>
/// <param name="Argument">The mission identifier or invitation code.</param>
public sealed record WebsiteMissionRequest(string? LoginName, string? Password, string? CharacterName, string? Action = null, string? Argument = null);
