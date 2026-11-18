// <copyright file="WebsiteAccountRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

/// <summary>Credentials for a player's own persisted account overview and bounded coin history.</summary>
/// <param name="LoginName">The account login name.</param>
/// <param name="Password">The current password, never returned or logged.</param>
/// <param name="Page">A zero-based page of twenty grants, up to the latest thousand.</param>
public sealed record WebsiteAccountRequest(string? LoginName, string? Password, int Page = 0);
