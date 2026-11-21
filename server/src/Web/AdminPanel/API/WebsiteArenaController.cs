// <copyright file="WebsiteArenaController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic.PlugIns.Arena;
using HNMO.Interfaces;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// The HNMO Arena's ranked 1v1 leaderboards for the public website (all time and this Sydney week). Only the Arena
/// program's admin panel has them; the website asks it through the web server's /website/arena.
/// </summary>
[Route("api/website/arena")]
[AllowAnonymous]
public sealed class WebsiteArenaController(IPersistenceContextProvider contextProvider) : Controller
{
    /// <summary>The leaderboards.</summary>
    /// <returns>Plain JSON: enabled, week, allTime, thisWeek.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        if (!HnmoSeasonInstance.IsArena)
        {
            return this.Content("{\"enabled\":false}", "application/json");
        }

        var cfg = await PlugInSettings.ReadActiveAsync<ArenaConfiguration>(contextProvider, typeof(ArenaPlugIn), () => new ArenaConfiguration(), this.HttpContext.RequestAborted).ConfigureAwait(false)
            ?? new ArenaConfiguration();
        var json = ArenaDuels.LeaderboardJson(ArenaStore.Open(cfg.FileName));
        return this.Content("{\"enabled\":true," + json[1..], "application/json");
    }
}
