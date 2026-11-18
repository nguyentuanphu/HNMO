// <copyright file="WebsiteController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic.PlugIns.Vip;
using HNMO.GameServer;
using HNMO.Interfaces;
using HNMO.Persistence.Worlds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Publishes an allowlisted snapshot of player-facing game configuration.</summary>
/// <param name="servers">The running game servers.</param>
/// <param name="worldRegistry">Optional multi-world registry.</param>
[ApiController]
[AllowAnonymous]
[Route("api/website")]
public sealed class WebsiteController(IDictionary<int, IGameServer> servers, WorldRegistryService? worldRegistry = null) : ControllerBase
{
    /// <summary>Reads configured worlds grouped with their sub-servers and Sydney opening times.</summary>
    /// <returns>Grouped worlds list.</returns>
    [HttpGet("worlds")]
    public IActionResult GetWorlds()
    {
        var registry = worldRegistry ?? new WorldRegistryService();
        var running = servers.Values.OfType<IGameServer>().OrderBy(s => s.Id).ToArray();
        var worlds = registry.BuildGroupedSnapshot(running, DateTimeOffset.UtcNow);
        if (this.HttpContext is not null)
        {
            this.Response.Headers.CacheControl = "no-store";
        }

        return this.Ok(new
        {
            updatedAt = DateTimeOffset.UtcNow,
            worlds,
        });
    }

    /// <summary>Reads current rates, maps, classes and event rules without account data.</summary>
    /// <returns>A fresh snapshot, or 503 while no game server is available.</returns>
    [HttpGet]
    public IActionResult Get()
    {
        var running = servers.Values.OfType<GameServer>().OrderBy(server => server.Context.Id).ToArray();
        if (running.Length == 0)
        {
            return this.StatusCode(503);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var registry = worldRegistry ?? new WorldRegistryService();
        var worlds = registry.BuildGroupedSnapshot(running.Cast<IGameServer>(), nowUtc);
        var worldByServerId = worlds
            .SelectMany(w => w.Servers.Select(s => (ServerId: s.Id, World: w)))
            .ToDictionary(x => x.ServerId, x => x.World);

        if (this.HttpContext is not null)
        {
            this.Response.Headers.CacheControl = "no-store";
        }

        return this.Ok(new
        {
            updatedAt = nowUtc,
            worlds,
            servers = running.Select(server =>
            {
                var context = server.Context;
                var config = context.Configuration;
                var (dropRate, zenRate) = GameServerRateStore.OpenDefault().GetRates(context.Id);
                worldByServerId.TryGetValue(context.Id, out var ownerWorld);
                return new
                {
                    id = context.Id,
                    description = server.Description,
                    vipOnly = GameServerRateStore.IsVipOnly(context),
                    pvpEnabled = context.PvpEnabled,
                    worldId = ownerWorld?.WorldId ?? "main",
                    worldNameEn = ownerWorld?.NameEn ?? "World 1",
                    worldNameVi = ownerWorld?.NameVi ?? "Thế giới 1",
                    isOpen = ownerWorld?.IsOpen ?? true,
                    openingDateUtc = ownerWorld?.OpeningDateUtc,
                    openingDateSydney = ownerWorld?.OpeningDateSydney ?? string.Empty,
                    players = context.PlayerCount,
                    capacity = context.ServerConfiguration.MaximumPlayers,
                    config.ExperienceRate,
                    config.MasterExperienceRate,
                    dropRate,
                    zenRate,
                    config.MaximumLevel,
                    config.MaximumMasterLevel,
                    config.MaximumPartySize,
                    config.MaximumCharactersPerAccount,
                    classes = config.CharacterClasses.Where(item => item.IsActive && item.CanGetCreated)
                        .Select(item => new { id = item.Number, name = item.Name.Value, requiredLevel = item.LevelRequirementByCreation }),
                    maps = context.ServerConfiguration.Maps.Where(item => item.IsActive)
                        .Select(item => new { id = item.Number, name = item.Name.Value, item.ExpMultiplier }),
                    events = config.MiniGameDefinitions.Where(item => item.IsActive)
                        .Select(item => new
                        {
                            type = item.Type.ToString(),
                            typeName = new { en = EventTypeName(item.Type.ToString(), "en"), vi = EventTypeName(item.Type.ToString(), "vi") },
                            name = item.Name.Value,
                            description = item.Description.Value,
                            level = item.GameLevel,
                            minimumLevel = item.MinimumCharacterLevel,
                            maximumLevel = item.MaximumCharacterLevel,
                            durationSeconds = item.GameDuration.TotalSeconds,
                            capacity = item.MaximumPlayerCount,
                            item.EntranceFee,
                        }),
                };
            }),
        });
    }

    /// <summary>
    /// The event's name in a language, from the server's translations of its start plug-in
    /// ("BloodCastleStartPlugIn_Name"); the type itself when there is none.
    /// </summary>
    private static string EventTypeName(string type, string language)
    {
        var name = HNMO.GameLogic.Properties.PlugInResources.ResourceManager.GetString($"{type}StartPlugIn_Name", System.Globalization.CultureInfo.GetCultureInfo(language));
        return string.IsNullOrWhiteSpace(name) || name.EndsWith("PlugIn", StringComparison.Ordinal) ? type : name;
    }
}
