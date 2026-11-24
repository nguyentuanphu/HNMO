// <copyright file="WebsiteContentController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using HNMO.GameLogic.PlugIns;
using HNMO.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

/// <summary>Publishes only the host's explicit website content fields.</summary>
/// <param name="persistence">The host settings provider.</param>
/// <param name="cache">A short-lived cache for public content.</param>
[ApiController]
[AllowAnonymous]
[Route("api/website/content")]
public sealed class WebsiteContentController(IPersistenceContextProvider persistence, IMemoryCache cache) : ControllerBase
{
    /// <summary>Reads the published roadmap, about, terms, and privacy text.</summary>
    /// <returns>Only the four sections and their two public translations.</returns>
    [HttpGet]
    public async ValueTask<IActionResult> GetAsync()
    {
        this.Response.Headers.CacheControl = "no-store";
        var content = await cache.GetOrCreateAsync(nameof(WebsiteContentController), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
            var settings = await PlugInSettings.ReadActiveAsync(persistence, typeof(WebsitePlugIn), () => new WebsiteConfiguration(), this.HttpContext.RequestAborted).ConfigureAwait(false);
            return Build(settings ?? new());
        }).ConfigureAwait(false);

        return this.Ok(content);
    }

    /// <summary>Builds the public projection without serializing the settings object.</summary>
    /// <param name="settings">The active Website settings.</param>
    /// <returns>The allowlisted public fields.</returns>
    internal static object Build(WebsiteConfiguration settings) => new
    {
        roadmap = new { en = Text(settings.ContentRoadmapEn), vi = Text(settings.ContentRoadmapVi) },
        about = new { en = Text(settings.ContentAboutEn), vi = Text(settings.ContentAboutVi) },
        terms = new { en = Text(settings.ContentTermsEn), vi = Text(settings.ContentTermsVi) },
        privacy = new { en = Text(settings.ContentPrivacyEn), vi = Text(settings.ContentPrivacyVi) },
    };

    private static string Text(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        return trimmed[..Math.Min(trimmed.Length, WebsiteConfiguration.MaximumPublicContentLength)];
    }
}
