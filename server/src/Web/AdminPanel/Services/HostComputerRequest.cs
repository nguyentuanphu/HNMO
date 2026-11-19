// <copyright file="HostComputerRequest.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Services;

using System.Net;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Tells whether a request comes from the person at the host computer, not through the website's admin path or
/// another proxy (audit 2.5). The host settings change ports, paths, the database password and the licence, so only
/// the host computer may change them.
/// </summary>
public static class HostComputerRequest
{
    private static readonly string[] ForwardingHeaders =
    [
        "X-Forwarded-For", "X-Forwarded-Host", "X-Forwarded-Proto", "Forwarded", "X-Real-IP", "CF-Connecting-IP", "True-Client-IP", "Via",
    ];

    /// <summary>Whether the request comes straight from this computer.</summary>
    /// <param name="context">The request, or <c>null</c> when unknown (then it's not).</param>
    /// <returns><c>true</c> for a direct request from this computer to a local host name.</returns>
    public static bool IsFromHostComputer(HttpContext? context)
    {
        if (context?.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote))
        {
            return false;
        }

        var host = context.Request.Host.Host;
        if (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            && !(IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address)))
        {
            return false;
        }

        return !ForwardingHeaders.Any(context.Request.Headers.ContainsKey);
    }
}
