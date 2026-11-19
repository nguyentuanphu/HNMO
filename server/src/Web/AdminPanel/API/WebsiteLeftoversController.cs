// <copyright file="WebsiteLeftoversController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic.PlugIns.CombiningLeftovers;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Public Combining Leftovers &amp; Surplus Material Exchange endpoint (<c>GET /api/website/leftovers</c>).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/leftovers")]
public sealed class WebsiteLeftoversController(IPersistenceContextProvider persistence) : ControllerBase
{
    /// <summary>Gets configured leftover upgrade recipes and surplus exchange offers.</summary>
    /// <returns>Plain-ASCII JSON; <c>"enabled":false</c> when disabled.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<CombiningLeftoversConfiguration>(
            persistence,
            typeof(CombiningLeftoversPlugIn),
            () => new CombiningLeftoversConfiguration(),
            cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        var engine = new CombiningLeftoversEngine(configuration, CombiningLeftoversStore.Open(configuration.FileName), enabled: true);
        return this.Content(CombiningLeftoversStateWriter.WritePublic(engine), "application/json");
    }
}
