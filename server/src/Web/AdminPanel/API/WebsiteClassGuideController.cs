// <copyright file="WebsiteClassGuideController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic;
using HNMO.GameLogic.PlugIns;
using HNMO.Interfaces;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

/// <summary>Serves the selected class from the configuration used by a running game server.</summary>
/// <param name="servers">The running game servers.</param>
/// <param name="persistence">The saved website configuration.</param>
/// <param name="cache">Short-lived class projections.</param>
[ApiController]
[AllowAnonymous]
[Route("api/website/class-guide")]
public sealed class WebsiteClassGuideController(IDictionary<int, IGameServer> servers, IPersistenceContextProvider persistence, IMemoryCache cache) : ControllerBase
{
    private const int MaximumServers = 32;

    /// <summary>Gets available servers and classes, with details for one active class.</summary>
    /// <param name="serverId">A running server identifier, or the default server when omitted.</param>
    /// <param name="classId">An active class number, or the first creatable class when omitted.</param>
    /// <returns>A bounded, player-facing configuration snapshot.</returns>
    [HttpGet]
    public async ValueTask<IActionResult> GetAsync(int? serverId = null, int? classId = null)
    {
        this.Response.Headers.CacheControl = "no-store";
        var config = await PlugInSettings.ReadActiveAsync(persistence, typeof(WebsitePlugIn), () => new WebsiteConfiguration(), this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (config is not { ClassGuideEnabled: true })
        {
            return this.Ok(new { enabled = false });
        }

        var running = servers.Values.Where(server => server is IGameServerContextProvider)
            .OrderBy(server => server.Id).Take(MaximumServers).ToArray();
        if (running.Length == 0)
        {
            return this.StatusCode(503);
        }

        var selected = serverId is { } id ? running.FirstOrDefault(server => server.Id == id) : running[0];
        if (selected is not IGameServerContextProvider provider)
        {
            return this.NotFound();
        }

        var world = provider.Context.Configuration;
        var classes = WebsiteClassGuideProjection.AvailableClasses(world);
        var characterClass = classId is { } number ? classes.FirstOrDefault(item => item.Number == number)
            : classes.FirstOrDefault(item => item.CanGetCreated) ?? classes.FirstOrDefault();
        if (characterClass is null && classId.HasValue)
        {
            return this.NotFound();
        }

        var details = characterClass is null ? null : cache.GetOrCreate((nameof(WebsiteClassGuideController), selected.Id, characterClass.Number), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return WebsiteClassGuideProjection.Build(world, characterClass);
        });
        return this.Ok(new
        {
            enabled = true,
            serverId = selected.Id,
            servers = running.Select(server => new { id = server.Id, name = WebsiteClassGuideProjection.LimitText(server.Description) }).ToArray(),
            classes = classes.Select(WebsiteClassGuideProjection.Class).ToArray(),
            details,
        });
    }
}
