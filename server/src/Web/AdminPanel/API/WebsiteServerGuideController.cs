// <copyright file="WebsiteServerGuideController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.MuHelper;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.PlugIns.Vip;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

/// <summary>Read-only, briefly cached server facts for the public home page.</summary>
/// <param name="servers">Running game servers.</param>
/// <param name="persistence">The host configuration source.</param>
/// <param name="cache">The bounded-lifetime public response cache.</param>
[ApiController]
[AllowAnonymous]
[Route("api/website/server-guide")]
public sealed class WebsiteServerGuideController(IDictionary<int, IGameServer> servers, IPersistenceContextProvider persistence, IMemoryCache cache) : ControllerBase
{
    // A fixed cache key and response cap cannot grow with visitor-controlled queries.
    private const string CacheKey = "website-server-guide";
    private const int MaximumServers = 32;

    /// <summary>Reads allowlisted rules and offers without opening any player data.</summary>
    /// <returns>The public facts, or 503 when no game server is available.</returns>
    [HttpGet]
    public async ValueTask<IActionResult> GetAsync()
    {
        var running = servers.Values.OfType<GameServer>().OrderBy(server => server.Context.Id).Take(MaximumServers).ToArray();
        if (running.Length == 0)
        {
            this.Response.Headers.CacheControl = "no-store";
            return this.StatusCode(503);
        }

        var snapshot = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            var settings = await PlugInSettings.ReadActiveAsync<WebsiteConfiguration>(persistence, typeof(WebsitePlugIn), this.HttpContext.RequestAborted).ConfigureAwait(false) ?? new();
            return new
            {
                updatedAt = DateTimeOffset.UtcNow,
                clientRules = new { en = settings.ServerClientRulesEn, vi = settings.ServerClientRulesVi },
                notForSale = new { en = settings.ServerNotForSaleEn, vi = settings.ServerNotForSaleVi },
                servers = running.Select(server => new { id = server.Context.Id, facts = Build(server.Context) }).ToArray(),
            };
        }).ConfigureAwait(false);
        this.Response.Headers.CacheControl = "public,max-age=15";
        return this.Ok(snapshot);
    }

    private static object Build(IGameContext context)
        => WebsiteServerGuideProjection.Build(
            context.Configuration,
            context.FeaturePlugIns.GetPlugIn<MuHelperFeaturePlugIn>()?.Configuration,
            VipPlugIn.Get(context)?.Configuration,
            context.FeaturePlugIns.GetPlugIn<MultiVaultPlugIn>()?.Configuration,
            context.FeaturePlugIns.GetPlugIn<RateTuningPlugIn>()?.Configuration,
            context.PlugInManager.GetActivePlugInsOf<ICashShopProductDeliveryPlugIn>().OfType<InventoryExtensionDeliveryPlugIn>().FirstOrDefault()?.Configuration);
}
