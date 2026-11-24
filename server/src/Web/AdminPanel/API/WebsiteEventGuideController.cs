// <copyright file="WebsiteEventGuideController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.Interfaces;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

/// <summary>Publishes bounded schedules and quest definitions from the running game servers.</summary>
/// <param name="servers">The host's running game servers.</param>
/// <param name="persistence">The saved website and timetable settings.</param>
/// <param name="cache">The short-lived guide snapshot cache.</param>
/// <param name="clock">The clock used to calculate event occurrences.</param>
[ApiController]
[AllowAnonymous]
[Route("api/website/event-guide")]
public sealed class WebsiteEventGuideController(IDictionary<int, IGameServer> servers, IPersistenceContextProvider persistence, IMemoryCache cache, TimeProvider clock) : ControllerBase
{
    private const int MaximumServers = 32;

    /// <summary>Gets the active events and NPC quests for the public guide.</summary>
    /// <param name="serverId">An optional running server identifier.</param>
    /// <returns>The selected server's configuration or an explicit disabled response.</returns>
    [HttpGet]
    public async ValueTask<IActionResult> GetAsync(int? serverId = null)
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var site = await PlugInSettings.ReadActiveAsync(persistence, typeof(WebsitePlugIn), () => new WebsiteConfiguration(), cancellationToken).ConfigureAwait(false);
        if (site is not { EventGuideEnabled: true })
        {
            return this.Ok(new { enabled = false });
        }

        var running = servers.Values.Where(server => server is IGameServerContextProvider).OrderBy(server => server.Id).Take(MaximumServers).ToArray();
        if (running.Length == 0)
        {
            return this.StatusCode(503);
        }

        var selected = serverId is { } id ? running.FirstOrDefault(server => server.Id == id) : running[0];
        if (selected is not IGameServerContextProvider provider)
        {
            return this.NotFound();
        }

        var context = provider.Context;
        var guide = await cache.GetOrCreateAsync((nameof(WebsiteEventGuideController), context.Id), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            var timetable = await PlugInSettings.ReadActiveAsync<TimetableConfiguration>(persistence, typeof(TimetablePlugIn), cancellationToken).ConfigureAwait(false) ?? new();
            var now = clock.GetUtcNow().UtcDateTime;
            var sources = TimetableSourceCollector.CollectFromContext(context, timetable, now, context.Id, ((IGameServerContextProvider)running[0]).Context.Id);
            var schedule = TimetableEngine.BuildSnapshot(timetable, true, now, sources, []);
            return WebsiteEventGuideProjection.Build(context.Configuration, context.ServerConfiguration.Maps, schedule, sources);
        }).ConfigureAwait(false);
        return this.Ok(new
        {
            enabled = true,
            serverId = context.Id,
            servers = running.Select(server => new { id = server.Id, name = WebsiteEventGuideProjection.Text(server.Description) }).ToArray(),
            guide,
        });
    }
}
