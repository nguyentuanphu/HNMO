// <copyright file="WebsiteOnboardingController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Onboarding;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// The website's "New players" section: the steps of the new-player guide with their rewards and the starter kit
/// (read-only, no player data), from the saved configuration of the <see cref="OnboardingPlugIn"/>.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/onboarding")]
public sealed class WebsiteOnboardingController(
    IPersistenceContextProvider persistence,
    IDataSource<GameConfiguration> gameConfigurationSource,
    ILogger<WebsiteOnboardingController> logger) : ControllerBase
{
    /// <summary>Gets the steps and the starter kit.</summary>
    /// <returns>The JSON; "enabled": false when the plug-in is off.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var cancellationToken = this.HttpContext.RequestAborted;
        var configuration = await PlugInSettings.ReadActiveAsync<OnboardingConfiguration>(persistence, typeof(OnboardingPlugIn), () => new OnboardingConfiguration(), cancellationToken).ConfigureAwait(false);
        if (configuration is null)
        {
            return this.Content("{\"v\":1,\"enabled\":false}", "application/json");
        }

        Func<short, short, string?>? itemName = null;
        var classes = new List<(int Number, string Name)>();
        try
        {
            var gameConfiguration = await gameConfigurationSource.GetOwnerAsync(Guid.Empty, cancellationToken).ConfigureAwait(false);
            var names = gameConfiguration.Items.GroupBy(i => (i.Group, i.Number)).ToDictionary(g => g.Key, g => g.First().Name.ValueInNeutralLanguage);
            itemName = (group, number) => names.TryGetValue(((byte)group, number), out var name) ? name : null;
            classes.AddRange(gameConfiguration.CharacterClasses.Select(c => ((int)c.Number, c.Name.ValueInNeutralLanguage ?? string.Empty)));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "The item and class names for the website's onboarding weren't read.");
        }

        return this.Content(OnboardingStateWriter.WritePublic(configuration, itemName, classes), "application/json");
    }
}
