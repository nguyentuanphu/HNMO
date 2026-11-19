// <copyright file="WebsiteGuildsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Guilds;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// The website's guilds (read-only, no account data): this week's guild ladder, last week's winners, every guild's level
/// and member count, the castle siege results, the levels with their buffs and the ladder rewards. The settings come
/// from the saved configuration of the <see cref="GuildsPlugIn"/>, the state from its store (shared with the game
/// servers of this process).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/guilds")]
public sealed class WebsiteGuildsController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILogger<WebsiteGuildsController> logger) : ControllerBase
{
    /// <summary>Gets the guilds.</summary>
    /// <returns>The JSON; "enabled": false when the plug-in is off.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<GuildsConfiguration>(persistence, typeof(GuildsPlugIn), () => new GuildsConfiguration(), cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        var engine = new GuildEngine(configuration, GuildStore.Open(configuration.FileName));
        var now = DateTime.UtcNow;
        Dictionary<Guid, int>? existing = null;
        try
        {
            var ids = engine.AllGuilds(now).Take(60).Select(g => g.GuildId).Union(engine.Ladder(now, 20).Select(e => e.GuildId)).ToList();
            using var guilds = persistence.CreateNewGuildContext();
            var names = await guilds.GetPersistentGuildNamesAsync(ids).ConfigureAwait(false);
            existing = new Dictionary<Guid, int>();
            foreach (var id in names.Keys)
            {
                existing[id] = (await guilds.GetMemberNamesAsync(id).ConfigureAwait(false)).Count;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The guilds and their members for the website weren't read.");
        }

        Func<short, short, string?>? itemName = null;
        try
        {
            var gameConfiguration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
            var items = gameConfiguration.Items.GroupBy(i => (i.Group, i.Number)).ToDictionary(g => g.Key, g => g.First().Name.ValueInNeutralLanguage);
            itemName = (group, number) => items.TryGetValue(((byte)group, number), out var name) ? name : null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The item names for the website's guilds weren't read.");
        }

        return this.Content(GuildStateWriter.WritePublic(engine, now, existing, itemName), "application/json");
    }
}
