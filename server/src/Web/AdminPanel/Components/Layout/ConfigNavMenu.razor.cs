// <copyright file="ConfigNavMenu.razor.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.Components.Layout;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using HNMO.AttributeSystem;
using HNMO.DataModel;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Configuration.Quests;
using HNMO.Web.AdminPanel.Properties;

/// <summary>
/// The menu part with the game configuration, grouped by topic, so that every kind of game data has a direct link.
/// </summary>
public partial class ConfigNavMenu
{
    /// <summary>
    /// Gets or sets the id of the game configuration.
    /// </summary>
    [Parameter]
    [Required]
    public Guid GameConfigurationId { get; set; }

    /// <summary>
    /// Gets the relative link to the generic grid editor of the type.
    /// </summary>
    /// <typeparam name="T">The edited type.</typeparam>
    /// <returns>The link.</returns>
    internal static string Grid<T>() => $"edit-config-grid/{typeof(T).FullName}/";

    /// <summary>
    /// Gets a link to the generic grid editor of the type, with its pluralized caption.
    /// </summary>
    /// <typeparam name="T">The edited type.</typeparam>
    /// <param name="icon">The icon class.</param>
    /// <returns>The link.</returns>
    internal static NavEntry GridEntry<T>(string icon) => new(typeof(T).GetPluralizedTypeCaption(), Grid<T>(), icon);

    private IReadOnlyList<NavGroup> GetGroups() =>
    [
        new("characters", Resources.NavCharactersAndSkills, "oi-person",
        [
            new(Resources.CharacterClasses, Grid<CharacterClass>(), "oi-people"),
            new(Resources.Skills, Grid<Skill>(), "oi-bolt"),
            GridEntry<MasterSkillRoot>("oi-fork"),
            GridEntry<MagicEffectDefinition>("oi-aperture"),
            GridEntry<AttributeDefinition>("oi-list-rich"),
        ]),
        new("items", Resources.Items, "oi-box",
        [
            new(Resources.Items, Grid<ItemDefinition>(), "oi-box"),
            GridEntry<ItemOptionDefinition>("oi-tag"),
            GridEntry<ItemOptionType>("oi-tags"),
            GridEntry<ItemSetGroup>("oi-layers"),
            GridEntry<ItemOptionCombinationBonus>("oi-plus"),
            GridEntry<ItemLevelBonusTable>("oi-graph"),
            GridEntry<ItemStatProfile>("oi-spreadsheet"),
            GridEntry<ItemPriceDefinition>("oi-dollar"),
            GridEntry<ItemSlotType>("oi-grid-three-up"),
            new(Resources.JewelMixes, Grid<JewelMix>(), "oi-beaker"),
            GridEntry<ItemCrafting>("oi-wrench"),
        ]),
        new("world", Resources.NavWorldAndMonsters, "oi-map",
        [
            new(Resources.GameMaps, Grid<GameMapDefinition>(), "oi-map"),
            new(Resources.Monsters, Grid<MonsterDefinition>(), "oi-bug"),
            new(Resources.WarpList, Grid<WarpInfo>(), "oi-transfer"),
            new(Resources.MiniGames, Grid<MiniGameDefinition>(), "oi-timer"),
            GridEntry<QuestDefinition>("oi-book"),
            new(Resources.MapEditor, "map-editor", "oi-globe"),
            new(Resources.TerrainEditor, "terrain-editor", "oi-brush"),
        ]),
        new("economy", Resources.NavShopsAndDrops, "oi-cart",
        [
            new(Resources.MerchantStores, "merchants/", "oi-cart"),
            new(Resources.CashShopCatalog, "cash-shop-catalog", "oi-credit-card"),
            new(Resources.DropItemGroups, Grid<DropItemGroup>(), "oi-random"),
            new(Resources.ItemDrops, "edit-item-drops", "oi-data-transfer-download"),
        ]),
        new("rules", Resources.NavRulesAndEvents, "oi-shield",
        [
            new(Resources.General, $"edit-config/{typeof(GameConfiguration).FullName}/{this.GameConfigurationId}/hide-collections", "oi-dial"),
            GridEntry<DuelConfiguration>("oi-target"),
            GridEntry<CastleSiegeConfiguration>("oi-shield"),
            GridEntry<CashShopConfiguration>("oi-dollar"),
            new(Resources.ContentActivation, "content-activation", "oi-circle-check"),
            new(Resources.Plugins, "plugins", "oi-puzzle-piece"),
            new(Resources.ChatCommands, "chat-commands", "oi-terminal"),
        ]),
        new("advanced", Resources.NavAdvanced, "oi-cog",
        [
            new(Resources.System, $"edit-config/{typeof(SystemConfiguration).FullName}/", "oi-cog"),
            new(Resources.GameClients, Grid<GameClientDefinition>(), "oi-monitor"),
            GridEntry<GameServerConfiguration>("oi-hard-drive"),
            GridEntry<AttributeRelationship>("oi-link-intact"),
            GridEntry<ConstValueAttribute>("oi-pin"),
            new(Resources.FullConfiguration, $"edit-config/{typeof(GameConfiguration).FullName}/", "oi-document", NavLinkMatch.All),
        ]),
    ];
}
