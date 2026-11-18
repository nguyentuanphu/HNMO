// <copyright file="WebsitePartySpotsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.PlugIns.PartyOptions;
using HNMO.Persistence;

/// <summary>
/// Public open hunting-spot party finder endpoint (<c>GET /api/website/party-spots</c>, never exposes passwords).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/party-spots")]
public sealed class WebsitePartySpotsController(IPersistenceContextProvider persistence) : ControllerBase
{
    /// <summary>Gets configured hunting spots and open party listings.</summary>
    /// <returns>Plain-ASCII JSON; <c>"enabled":false</c> when disabled.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<PartyOptionsConfiguration>(
            persistence,
            typeof(PartyOptionsPlugIn),
            () => new PartyOptionsConfiguration(),
            cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        var store = PartyOptionsStore.Open(configuration.FileName);
        return this.Content(PartyOptionsStateWriter.WritePublic(configuration, store), "application/json");
    }
}
