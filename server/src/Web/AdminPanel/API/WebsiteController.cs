// <copyright file="WebsiteController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameServer;
using HNMO.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Publishes an allowlisted snapshot of player-facing game configuration.</summary>
/// <param name="servers">The running game servers.</param>
[ApiController]
[AllowAnonymous]
[Route("api/website")]
public sealed class WebsiteController(IDictionary<int, IGameServer> servers) : ControllerBase
{
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

        this.Response.Headers.CacheControl = "no-store";
        return this.Ok(new
        {
            updatedAt = DateTimeOffset.UtcNow,
            servers = running.Select(server =>
            {
                var context = server.Context;
                var config = context.Configuration;
                return new
                {
                    id = context.Id,
                    players = context.PlayerCount,
                    capacity = context.ServerConfiguration.MaximumPlayers,
                    config.ExperienceRate,
                    config.MasterExperienceRate,
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
}
