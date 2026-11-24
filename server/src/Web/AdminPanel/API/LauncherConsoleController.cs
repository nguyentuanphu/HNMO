// <copyright file="LauncherConsoleController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using System.Net;
using HNMO.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>Opens the launcher console from the primary admin panel on its host.</summary>
[Authorize(Roles = "Administrator")]
public sealed class LauncherConsoleController : Controller
{
    private static readonly string[] ForwardedHeaders =
    [
        "X-HNMO-Website-Prefix", "X-Forwarded-For", "X-Forwarded-Host", "X-Forwarded-Proto",
        "Forwarded", "X-Real-IP", "CF-Connecting-IP", "True-Client-IP", "Via",
    ];

    /// <summary>Redirects a frame request to the local launcher after authenticating the administrator.</summary>
    /// <returns>The local console redirect or a forbidden response.</returns>
    [HttpGet("server-control/console-frame")]
    public IActionResult Open()
    {
        if (this.HttpContext.Connection.RemoteIpAddress is not { } remote
            || !IPAddress.IsLoopback(remote)
            || ForwardedHeaders.Any(this.Request.Headers.ContainsKey)
            || this.Request.Headers["Sec-Fetch-Dest"] != "iframe"
            || !Uri.TryCreate($"{this.Request.Scheme}://{this.Request.Host}", UriKind.Absolute, out var admin)
            || !admin.IsLoopback || admin.Scheme != "http"
            || !int.TryParse(Environment.GetEnvironmentVariable(HostSettingsStore.LauncherWebPortVariableName), out var port)
            || port is < 1 or > 65535)
        {
            return this.StatusCode(StatusCodes.Status403Forbidden);
        }

        var address = new UriBuilder("http", admin.Host, port, "/control/console")
        {
            Query = $"lang={CultureInfo.CurrentUICulture.TwoLetterISOLanguageName}",
        };
        this.Response.Headers.CacheControl = "no-store";
        return this.Redirect(address.Uri.AbsoluteUri);
    }
}
