// <copyright file="WebsiteWorldBossesController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.WorldBosses;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// The website's world bosses (read-only, no account data): the next spawn of each boss, its map and rewards, the
/// boss on the map now, the last kills with the top damage dealers, the announcements and the weekly rankings with last
/// week's winners. The settings come from the saved configuration of the <see cref="WorldBossesPlugIn"/>, the state
/// from its store (shared with the game servers of this process).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/world-bosses")]
public sealed class WebsiteWorldBossesController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILogger<WebsiteWorldBossesController> logger) : ControllerBase
{
    /// <summary>Gets the world bosses.</summary>
    /// <returns>The world bosses as JSON; an empty list when the plug-in is off.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<WorldBossesConfiguration>(persistence, typeof(WorldBossesPlugIn), () => new WorldBossesConfiguration(), cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"bosses\":[],\"off\":true}", "application/json");
        }

        Func<short, short, string?>? itemName = null;
        Func<short, string?>? mapName = null;
        try
        {
            var gameConfiguration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
            var items = gameConfiguration.Items.GroupBy(i => (i.Group, i.Number)).ToDictionary(g => g.Key, g => g.First().Name.ValueInNeutralLanguage);
            itemName = (group, number) => items.TryGetValue(((byte)group, number), out var name) ? name : null;
            var maps = gameConfiguration.Maps.GroupBy(m => m.Number).ToDictionary(g => g.Key, g => g.First().Name.ValueInNeutralLanguage);
            mapName = number => maps.TryGetValue(number, out var name) ? name : null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The item and map names for the website's world bosses weren't read.");
        }

        var engine = new WorldBossEngine(configuration, WorldBossStore.Open(configuration.FileName));
        return this.Content(WorldBossBoardWriter.WritePublic(engine, DateTime.UtcNow, mapName, itemName), "application/json");
    }
}
