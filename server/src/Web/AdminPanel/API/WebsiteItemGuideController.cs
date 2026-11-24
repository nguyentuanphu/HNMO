// <copyright file="WebsiteItemGuideController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.PlugIns;
using HNMO.Interfaces;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

/// <summary>Publishes paged item and mix facts from the selected running server.</summary>
/// <param name="servers">Running game servers.</param>
/// <param name="persistence">Saved website settings.</param>
/// <param name="cache">Short-lived per-server catalogue indexes.</param>
[ApiController]
[AllowAnonymous]
[Route("api/website/item-guide")]
public sealed class WebsiteItemGuideController(IDictionary<int, IGameServer> servers, IPersistenceContextProvider persistence, IMemoryCache cache) : ControllerBase
{
    private const int MaximumServers = 32;

    /// <summary>Gets a bounded page of items and mixes, with details for one selection of each.</summary>
    /// <param name="serverId">The optional running server ID.</param>
    /// <param name="group">The active item group.</param>
    /// <param name="page">The requested item page, starting with one.</param>
    /// <param name="number">An item number in the active group.</param>
    /// <param name="mixPage">The requested mix page, starting with one.</param>
    /// <param name="mixIndex">The index of the selected recipe in the current catalogue.</param>
    /// <returns>A public catalogue page or the disabled state.</returns>
    [HttpGet]
    public async ValueTask<IActionResult> GetAsync(int? serverId = null, int? group = null, int page = 1, int? number = null, int mixPage = 1, int? mixIndex = null)
    {
        this.Response.Headers.CacheControl = "no-store";
        if (group is < 0 or > byte.MaxValue || number is < 0 or > short.MaxValue || mixIndex < 0 || page < 1 || mixPage < 1)
        {
            return this.BadRequest();
        }

        var config = await PlugInSettings.ReadActiveAsync(persistence, typeof(WebsitePlugIn), () => new WebsiteConfiguration(), this.HttpContext.RequestAborted).ConfigureAwait(false);
        if (config is not { ItemGuideEnabled: true })
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
        var catalogue = cache.GetOrCreate((nameof(WebsiteItemGuideController), selected.Id), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            return new WebsiteItemGuideCatalogue(context.Configuration, context.ServerConfiguration.Maps,
                context.PlugInManager.GetActivePlugInsOf<IItemDropPlugIn>().Any(plugin => plugin is ItemBoxDroppedPlugIn));
        })!;
        var result = catalogue.Read(group, page, number, mixPage, mixIndex);
        if (result is null)
        {
            return this.NotFound();
        }

        return this.Ok(new
        {
            enabled = true,
            serverId = selected.Id,
            servers = running.Select(server => new { id = server.Id, name = WebsiteItemGuideCatalogue.Text(server.Description) }).ToArray(),
            guide = result,
        });
    }
}
