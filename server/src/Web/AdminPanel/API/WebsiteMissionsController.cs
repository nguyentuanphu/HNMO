// <copyright file="WebsiteMissionsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// The website's list of today's daily and this week's weekly missions with their rewards (read-only, no player data),
/// from the saved configuration of the <see cref="MissionsPlugIn"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/missions")]
public sealed class WebsiteMissionsController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILogger<WebsiteMissionsController> logger) : ControllerBase
{
    /// <summary>Gets the missions.</summary>
    /// <returns>The missions as JSON; an empty list when the missions are off.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<MissionsConfiguration>(persistence, typeof(MissionsPlugIn), () => new MissionsConfiguration(), cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"missions\":[]}", "application/json");
        }

        Func<short, short, string?>? itemName = null;
        try
        {
            var gameConfiguration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
            var names = gameConfiguration.Items.GroupBy(i => (i.Group, i.Number)).ToDictionary(g => g.Key, g => g.First().Name.ValueInNeutralLanguage);
            itemName = (group, number) => names.TryGetValue(((byte)group, number), out var name) ? name : null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The item names for the website's missions weren't read.");
        }

        return this.Content(MissionBoardWriter.WritePublic(configuration, DateTime.UtcNow, itemName), "application/json");
    }
}
