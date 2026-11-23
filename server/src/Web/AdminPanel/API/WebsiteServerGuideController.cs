// <copyright file="WebsiteServerGuideController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic;
using HNMO.GameLogic.CashShop;
using HNMO.GameLogic.MuHelper;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.ActivityLog;
using HNMO.GameLogic.PlugIns.AntiCheat;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.PlugIns.Vip;
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
        var running = servers.Values.OfType<IGameServerContextProvider>().OrderBy(server => server.Context.Id).Take(MaximumServers).ToArray();
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
                servers = running.Select(server => new
                {
                    id = server.Context.Id,
                    facts = Build(server.Context),
                    extras = Extras(server.Context),
                    realmRules = RealmRules(server.Context),
                }).ToArray(),
            };
        }).ConfigureAwait(false);
        this.Response.Headers.CacheControl = "public,max-age=15";
        return this.Ok(snapshot);
    }

    private static object Extras(IGameServerContext context)
    {
        var npcs = context.ServerConfiguration.Maps.Where(map => map.IsActive)
            .SelectMany(map => map.MonsterSpawns).Where(spawn => spawn.Quantity > 0)
            .Select(spawn => spawn.MonsterDefinition).OfType<HNMO.DataModel.Configuration.MonsterDefinition>()
            .Where(npc => npc.IsActive && (npc.Quests.Count > 0 || npc.ItemCraftings.Count > 0)).Distinct().ToArray();
        var activity = ActivityLogPlugIn.Get(context);
        var activityConfiguration = activity?.Configuration ?? new ActivityLogConfiguration();
        return WebsiteServerGuideExtrasProjection.Build(
            npcs,
            context.FeaturePlugIns.GetPlugIn<AntiCheatPlugIn>()?.Configuration?.Enabled == true,
            activity is not null && activityConfiguration.Enabled,
            activityConfiguration.OnlyValuableDropsAndPickups);
    }

    private static object Build(IGameContext context)
        => WebsiteServerGuideProjection.Build(
            context.Configuration,
            context.FeaturePlugIns.GetPlugIn<MuHelperFeaturePlugIn>()?.Configuration,
            VipPlugIn.Get(context)?.Configuration,
            context.FeaturePlugIns.GetPlugIn<MultiVaultPlugIn>()?.Configuration,
            context.FeaturePlugIns.GetPlugIn<RateTuningPlugIn>()?.Configuration,
            context.PlugInManager.GetActivePlugInsOf<ICashShopProductDeliveryPlugIn>().OfType<InventoryExtensionDeliveryPlugIn>().FirstOrDefault()?.Configuration);

    private static object RealmRules(IGameContext context)
    {
        var durations = context.Configuration.MiniGameDefinitions
            .Where(definition => definition is { IsActive: true, Type: HNMO.DataModel.Configuration.MiniGameType.BloodCastle }
                && definition.GameDuration > TimeSpan.Zero)
            .Select(definition => definition.GameDuration.TotalSeconds)
            .ToArray();
        return new
        {
            guildCreationMinimumLevel = GuildLimitsPlugIn.GetMinimumCreateLevel(context),
            bloodCastle = durations.Length == 0 ? null : new { minimumSeconds = durations.Min(), maximumSeconds = durations.Max() },
        };
    }
}
