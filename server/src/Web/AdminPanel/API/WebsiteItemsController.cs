// <copyright file="WebsiteItemsController.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HNMO.GameLogic.ItemDefinitions;

/// <summary>
/// Public read-only website API exposing admin-managed item definition overrides (<c>api/website/items</c>)
/// so the web client receives updated item names (EN/VI), sizes, levels, requirements, glow colors, and tooltip notes
/// without manual file editing.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/website/items")]
public sealed class WebsiteItemsController : ControllerBase
{
    /// <summary>
    /// Gets the active item definition overrides managed from the admin panel.
    /// </summary>
    /// <returns>The list of item overrides.</returns>
    [HttpGet]
    public IActionResult Get()
    {
        this.Response.Headers.CacheControl = "no-store";
        var all = ItemDefinitionStateStore.GetAll();
        var updatedAtUtc = all.Count == 0
            ? (DateTime?)null
            : all.Max(x => x.UpdatedAtUtc);

        var items = all.Select(x => new
        {
            group = x.Group,
            number = x.Number,
            name = BuildLocalizedMap(x.NameEn, x.NameVi),
            width = x.Width,
            height = x.Height,
            level = x.DropLevel,
            maximumItemLevel = x.MaximumItemLevel,
            durability = x.Durability,
            buyPrice = x.BuyPrice,
            dropsFromMonsters = x.DropsFromMonsters,
            requirements = new
            {
                level = x.LevelRequirement > 0 ? (int?)x.LevelRequirement : null,
                strength = x.StrengthRequirement > 0 ? (int?)x.StrengthRequirement : null,
                dexterity = x.AgilityRequirement > 0 ? (int?)x.AgilityRequirement : null,
                vitality = x.VitalityRequirement > 0 ? (int?)x.VitalityRequirement : null,
                energy = x.EnergyRequirement > 0 ? (int?)x.EnergyRequirement : null,
                leadership = x.LeadershipRequirement > 0 ? (int?)x.LeadershipRequirement : null,
            },
            glowColor = x.GlowColor,
            tooltip = string.IsNullOrWhiteSpace(x.TooltipTextEn) && string.IsNullOrWhiteSpace(x.TooltipTextVi)
                ? null
                : BuildLocalizedMap(x.TooltipTextEn, x.TooltipTextVi),
            modelSourceGroup = x.ModelSourceGroup,
            modelSourceNumber = x.ModelSourceNumber,
            updatedAtUtc = x.UpdatedAtUtc,
        }).ToList();

        return this.Ok(new
        {
            version = 1,
            updatedAtUtc,
            count = items.Count,
            items,
        });
    }

    private static Dictionary<string, string> BuildLocalizedMap(string en, string vi)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(en))
        {
            map["en"] = en.Trim();
        }

        if (!string.IsNullOrWhiteSpace(vi))
        {
            map["vi"] = vi.Trim();
        }

        return map;
    }
}
