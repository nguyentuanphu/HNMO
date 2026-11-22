// <copyright file="WebsiteServerGuideExtrasProjection.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;

/// <summary>Bounded public quest and crafting possibilities from NPCs configured on this server's active maps.</summary>
public static class WebsiteServerGuideExtrasProjection
{
    private const int MaximumRows = 32;
    private const int FullPercentage = 100;

    /// <summary>Builds public possibilities, keeping private logs and enforcement settings out of the response.</summary>
    /// <param name="npcs">Distinct active NPC definitions on active maps.</param>
    /// <param name="antiCheatEnabled">Whether the active anti-cheat feature is enabled.</param>
    /// <param name="activityLogEnabled">Whether the active activity recorder is enabled.</param>
    /// <param name="onlyValuableDrops">Whether drop/pickup recording excludes ordinary items.</param>
    /// <returns>The public configuration summary.</returns>
    public static object Build(IReadOnlyList<MonsterDefinition> npcs, bool antiCheatEnabled, bool activityLogEnabled, bool onlyValuableDrops)
    {
        var quests = npcs.SelectMany(npc => npc.Quests.Select(quest => new
        {
            nameEn = quest.Name.GetTranslation(CultureInfo.GetCultureInfo("en")),
            nameVi = quest.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")),
            npcEn = npc.Designation.GetTranslation(CultureInfo.GetCultureInfo("en")),
            npcVi = npc.Designation.GetTranslation(CultureInfo.GetCultureInfo("vi")),
            minimumLevel = quest.MinimumCharacterLevel,
            maximumLevel = quest.MaximumCharacterLevel,
            quest.RequiredStartMoney,
            classEn = quest.QualifiedCharacter?.Name.GetTranslation(CultureInfo.GetCultureInfo("en")),
            classVi = quest.QualifiedCharacter?.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")),
        })).Take(MaximumRows + 1).ToArray();
        var crafts = npcs.SelectMany(npc => npc.ItemCraftings.Where(WebsiteCraftingQuote.UsesSettings)
            .SelectMany(recipe => recipe.SimpleCraftingSettings!.ResultItems.Where(result => result.ItemDefinition is { IsActive: true } || result.Reference != 0).Select(result => Craft(npc, recipe, result))))
            .Take(MaximumRows + 1).ToArray();
        return new
        {
            systems = new { antiCheatEnabled, activityLogEnabled, onlyValuableDrops },
            quests = quests.Take(MaximumRows).ToArray(),
            questsTruncated = quests.Length > MaximumRows,
            crafts = crafts.Take(MaximumRows).ToArray(),
            craftsTruncated = crafts.Length > MaximumRows,
        };
    }

    private static object Craft(MonsterDefinition npc, ItemCrafting recipe, ItemCraftingResultItem result)
    {
        var settings = recipe.SimpleCraftingSettings!;
        return new
        {
            nameEn = recipe.Name.GetTranslation(CultureInfo.GetCultureInfo("en")),
            nameVi = recipe.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")),
            npcEn = npc.Designation.GetTranslation(CultureInfo.GetCultureInfo("en")),
            npcVi = npc.Designation.GetTranslation(CultureInfo.GetCultureInfo("vi")),
            itemEn = result.ItemDefinition?.Name.GetTranslation(CultureInfo.GetCultureInfo("en")),
            itemVi = result.ItemDefinition?.Name.GetTranslation(CultureInfo.GetCultureInfo("vi")),
            result.RandomMinimumLevel,
            result.RandomMaximumLevel,
            result.AddLevel,
            upgrade = result.Reference != 0,
            basePercent = settings.NpcPriceDivisor > 0 ? (int?)null : (int)settings.SuccessPercent,
            maximumPercent = settings.MaximumSuccessPercent > 0 ? Math.Min((int)settings.MaximumSuccessPercent, FullPercentage) : FullPercentage,
            fixedZen = settings.Money,
            zenPerFinalPercentage = settings.MoneyPerFinalSuccessPercentage,
            excellentChance = WebsiteCraftingQuote.UsesSettingsExcellentRoll(recipe) ? (int?)settings.ResultItemExcellentOptionChance : null,
            maximumExcellentOptions = settings.ResultItemMaxExcOptionCount,
        };
    }
}
