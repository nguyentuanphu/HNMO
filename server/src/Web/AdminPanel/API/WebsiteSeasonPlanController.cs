// <copyright file="WebsiteSeasonPlanController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.SeasonPlan;
using HNMO.Persistence;

/// <summary>
/// Public Season Plan timeline endpoint (<c>GET /api/website/season-plan</c>).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/season-plan")]
public sealed class WebsiteSeasonPlanController(IPersistenceContextProvider persistence) : ControllerBase
{
    /// <summary>Gets the Season Plan stages, unlock conditions, and live feature gates.</summary>
    /// <returns>Plain-ASCII JSON; <c>"enabled":false</c> when disabled.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<SeasonPlanConfiguration>(
            persistence,
            typeof(SeasonPlanPlugIn),
            () => new SeasonPlanConfiguration(),
            cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        var engine = new SeasonPlanEngine(configuration, SeasonPlanStore.Open(configuration.FileName), enabled: true);
        return this.Content(SeasonPlanStateWriter.WritePublic(engine, DateTime.UtcNow), "application/json");
    }
}
