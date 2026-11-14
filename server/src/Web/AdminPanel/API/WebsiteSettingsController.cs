// <copyright file="WebsiteSettingsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;

/// <summary>
/// The website's settings (the "Website" plug-in). The secret admin path only goes to the website on
/// this computer; visitors only ever get the Google client id, which is public by nature.
/// </summary>
[Route("api/website/settings")]
[AllowAnonymous]
public class WebsiteSettingsController : Controller
{
    private readonly IPersistenceContextProvider _contextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsiteSettingsController"/> class.
    /// </summary>
    /// <param name="contextProvider">The persistence context provider.</param>
    public WebsiteSettingsController(IPersistenceContextProvider contextProvider)
    {
        this._contextProvider = contextProvider;
    }

    /// <summary>
    /// What the website's pages may know: the Google client id (empty: no Google sign-in).
    /// </summary>
    /// <returns>The public settings.</returns>
    [HttpGet("public")]
    public async Task<IActionResult> PublicAsync()
    {
        this.Response.Headers.CacheControl = "no-cache";
        var config = await this.ReadAsync().ConfigureAwait(false);
        return this.Json(new { googleClientId = config.GoogleClientId.Trim() });
    }

    /// <summary>
    /// What the website itself needs: the admin path too. Only for the website process on this computer,
    /// never for a request it forwarded from a visitor.
    /// </summary>
    /// <returns>The settings, or 404 for anyone else.</returns>
    [HttpGet("host")]
    public async Task<IActionResult> HostAsync()
    {
        if (WebsitePrefixMiddleware.IsThroughWebsite(this.Request)
            || this.HttpContext.Connection.RemoteIpAddress is not { } remote
            || !IPAddress.IsLoopback(remote))
        {
            return this.NotFound();
        }

        this.Response.Headers.CacheControl = "no-store";
        var config = await this.ReadAsync().ConfigureAwait(false);
        return this.Json(new { adminPath = config.AdminPath.Trim(), googleClientId = config.GoogleClientId.Trim() });
    }

    private async Task<WebsiteConfiguration> ReadAsync() =>
        await PlugInSettings.ReadActiveAsync<WebsiteConfiguration>(this._contextProvider, typeof(WebsitePlugIn), this.HttpContext.RequestAborted).ConfigureAwait(false)
        ?? new WebsiteConfiguration();
}
