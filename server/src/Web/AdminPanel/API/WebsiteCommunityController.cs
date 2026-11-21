// <copyright file="WebsiteCommunityController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Community;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// The website's community section (read-only, no account data): the one-time coins for Discord and Zalo, the
/// community events which are shown there (without their codes) and the recent highlights. The settings come from the
/// saved configuration of the <see cref="CommunityPlugIn"/>, the state from its store (shared with the game servers of
/// this process).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/community")]
public sealed class WebsiteCommunityController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILogger<WebsiteCommunityController> logger) : ControllerBase
{
    /// <summary>Gets the community section.</summary>
    /// <returns>The JSON; "enabled": false when the plug-in is off.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<CommunityConfiguration>(persistence, typeof(CommunityPlugIn), () => new CommunityConfiguration(), cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
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
            logger.LogDebug(ex, "The item names for the website's community section weren't read.");
        }

        var engine = new CommunityEngine(configuration, CommunityStore.Open(configuration.FileName));
        return this.Content(CommunityStateWriter.WritePublic(engine, DateTime.UtcNow, itemName), "application/json");
    }
}
