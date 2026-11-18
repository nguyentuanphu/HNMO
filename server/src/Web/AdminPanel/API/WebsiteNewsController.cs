// <copyright file="WebsiteNewsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.News;
using HNMO.Persistence;

/// <summary>
/// Public News &amp; Announcements API (<c>GET /api/website/news</c>) used by the homepage,
/// login/character-select news panel, and public news feeds.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/news")]
public sealed class WebsiteNewsController(IPersistenceContextProvider persistence) : ControllerBase
{
    /// <summary>Gets currently published EN/VI news posts (excluding future-scheduled posts).</summary>
    /// <returns>Plain-ASCII JSON; <c>"enabled":false</c> when the plug-in or website feed is disabled.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        if (this.HttpContext is not null)
        {
            this.Response.Headers.CacheControl = "no-store";
        }

        var cancellationToken = this.HttpContext?.RequestAborted ?? CancellationToken.None;
        var configuration = await PlugInSettings.ReadActiveAsync<NewsConfiguration>(
            persistence,
            typeof(NewsPlugIn),
            () => new NewsConfiguration(),
            cancellationToken).ConfigureAwait(false);

        if (configuration is null || (!configuration.AllowWebsite && !configuration.ShowLoginPanel))
        {
            return this.Content("{\"v\":1,\"enabled\":false,\"allowWebsite\":false,\"showLoginPanel\":false,\"posts\":[]}", "application/json");
        }

        var store = NewsStore.Open(configuration.FileName);
        return this.Content(NewsStateWriter.WritePublic(configuration, store, DateTime.UtcNow), "application/json");
    }
}
