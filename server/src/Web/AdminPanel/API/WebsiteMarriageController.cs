// <copyright file="WebsiteMarriageController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic.PlugIns.Marriage;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Public read-only website endpoint exposing marriage rules and the couples list (<c>api/website/marriage</c>).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/marriage")]
public sealed class WebsiteMarriageController(IPersistenceContextProvider persistence) : ControllerBase
{
    /// <summary>Gets public marriage rules and registered couples.</summary>
    /// <returns>Plain-ASCII JSON payload.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var (_, isActive, saved) = await PlugInSettings.ReadStoredAsync<MarriageConfiguration>(
            persistence,
            typeof(MarriagePlugIn),
            this.HttpContext.RequestAborted).ConfigureAwait(false);
        var cfg = saved ?? new MarriageConfiguration();
        var engine = new MarriageEngine(cfg, MarriageStore.Open(cfg.FileName), isActive);
        return this.Content(MarriageStateWriter.WritePublic(engine, DateTime.UtcNow), "application/json");
    }
}
