// <copyright file="WebsiteGiftCodeAddress.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Net;
using Microsoft.AspNetCore.Http;

/// <summary>Reads gift-code network identities at the trusted local bridge boundary.</summary>
public static class WebsiteGiftCodeAddress
{
    /// <summary>Uses the bridge's single address only for a loopback peer; direct clients cannot supply it.</summary>
    /// <param name="context">The request context, before any untrusted forwarded-header processing.</param>
    /// <returns>The observed address, or null when the connection has none.</returns>
    public static string? Read(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null)
        {
            return null;
        }

        var peer = remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote;
        if (IPAddress.IsLoopback(peer)
            && IPAddress.TryParse(context.Request.Headers["X-HNMO-Client-Address"].ToString(), out var forwarded))
        {
            return (forwarded.IsIPv4MappedToIPv6 ? forwarded.MapToIPv4() : forwarded).ToString();
        }

        return peer.ToString();
    }
}
