// <copyright file="WebsiteGiftBoxRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

/// <summary>Own-account mailbox credentials and selections; no reward or account identifier is accepted.</summary>
/// <param name="LoginName">The player's exact login.</param>
/// <param name="Password">The current password, verified again under an offline collection lease.</param>
/// <param name="Offset">The bounded mailbox offset.</param>
/// <param name="LetterId">The existing letter to inspect or collect.</param>
/// <param name="CharacterId">The owned collection target.</param>
/// <param name="Language">English or Vietnamese.</param>
public sealed record WebsiteGiftBoxRequest(string LoginName, string Password, int Offset = 0, Guid? LetterId = null, Guid? CharacterId = null, string Language = "en");
