// <copyright file="WebsiteTopRacesController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.TopRaces;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// The website's top races (read-only, no account data): the running races with their tables and prizes, the races to
/// come and the past winners. The settings come from the saved configuration of the <see cref="TopRacesPlugIn"/>, the
/// state from its store (shared with the game servers of this process).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/top-races")]
public sealed class WebsiteTopRacesController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILogger<WebsiteTopRacesController> logger) : ControllerBase
{
    /// <summary>Gets the top races.</summary>
    /// <returns>The JSON; "enabled": false when the plug-in is off.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<TopRacesConfiguration>(persistence, typeof(TopRacesPlugIn), () => new TopRacesConfiguration(), cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        var engine = new TopRaceEngine(configuration, TopRaceStore.Open(configuration.FileName));
        Func<short, short, string?>? itemName = null;
        try
        {
            var gameConfiguration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
            var items = gameConfiguration.Items.GroupBy(i => (i.Group, i.Number)).ToDictionary(g => g.Key, g => g.First().Name.ValueInNeutralLanguage);
            itemName = (group, number) => items.TryGetValue(((byte)group, number), out var name) ? name : null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The item names for the website's top races weren't read.");
        }

        return this.Content(TopRaceStateWriter.WritePublic(engine, DateTime.UtcNow, itemName), "application/json");
    }
}
