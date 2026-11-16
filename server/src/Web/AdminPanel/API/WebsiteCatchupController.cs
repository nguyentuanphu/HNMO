// <copyright file="WebsiteCatchupController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.CatchupCap;
using HNMO.Persistence;

/// <summary>
/// Public Catch-up Level &amp; Daily Reset Cap API (<c>GET /api/website/catchup</c>).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/catchup")]
public sealed class WebsiteCatchupController(IPersistenceContextProvider persistence) : ControllerBase
{
    /// <summary>Gets today's Catch-up Level &amp; Daily Reset cap.</summary>
    /// <returns>Plain-ASCII JSON; <c>"enabled":false</c> when disabled.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<CatchupCapConfiguration>(
            persistence,
            typeof(CatchupCapPlugIn),
            () => new CatchupCapConfiguration(),
            cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        var engine = new CatchupCapEngine(configuration, CatchupCapStore.Open(configuration.FileName), enabled: true);
        return this.Content(CatchupCapStateWriter.WritePublic(engine, DateTime.UtcNow), "application/json");
    }
}
