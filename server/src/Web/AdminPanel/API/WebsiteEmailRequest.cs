// <copyright file="WebsiteEmailRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

/// <summary>An authenticated request for the account's own email confirmation.</summary>
/// <param name="LoginName">The account name.</param>
/// <param name="Password">The current game password.</param>
/// <param name="NewEmail">The requested mailbox, used only when sending.</param>
/// <param name="Code">The mailed code, used only when confirming.</param>
/// <param name="Language">English or Vietnamese for the email.</param>
public sealed record WebsiteEmailRequest(string? LoginName, string? Password, string? NewEmail = null, string? Code = null, string? Language = null);
