// <copyright file="WebsitePasswordChangeRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

/// <summary>Current account credentials and a replacement password; none are returned or logged.</summary>
/// <param name="LoginName">The account login name.</param>
/// <param name="Password">The current password.</param>
/// <param name="NewPassword">The requested replacement password.</param>
public sealed record WebsitePasswordChangeRequest(string? LoginName, string? Password, string? NewPassword);
