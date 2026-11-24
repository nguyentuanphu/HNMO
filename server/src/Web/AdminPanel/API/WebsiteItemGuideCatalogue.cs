// <copyright file="WebsiteItemGuideCatalogue.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.GameLogic.Attributes;
using HNMO.Interfaces;

/// <summary>Indexes active definitions once and projects bounded, settings-backed guides for public visitors.</summary>
public sealed class WebsiteItemGuideCatalogue
{
    private const int PageSize = 24;
    private const int MaximumSources = 24;
    private const int MaximumDetails = 16;
    private const int FullPercentage = 100;
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi");
    private readonly GameConfiguration _configuration;
    private readonly bool _boxesEnabled;
    private readonly ItemDefinition[] _items;
    private readonly ILookup<int, ItemDefinition> _groups;
    private readonly (GameMapDefinition Map, MonsterDefinition Monster, int? Level)[] _spawned;
    private readonly Recipe[] _mixes;
    private readonly ILookup<(byte Group, short Number), BoxInput> _boxInputs;

    /// <summary>Initializes a short-lived catalogue of currently active items and spawned monsters and NPCs.</summary>
    /// <param name="configuration">The loaded game definitions.</param>
    /// <param name="maps">The maps hosted by the selected server.</param>
    /// <param name="boxesEnabled">Whether the box-drop plug-in is active.</param>
    public WebsiteItemGuideCatalogue(GameConfiguration configuration, IEnumerable<GameMapDefinition> maps, bool boxesEnabled)
    {
        this._configuration = configuration;
        this._boxesEnabled = boxesEnabled;
        this._items = configuration.Items.Where(item => item.IsActive && item.Number >= 0).OrderBy(item => item.Group).ThenBy(item => item.Number)
            .DistinctBy(item => (item.Group, item.Number)).ToArray();
        this._groups = this._items.ToLookup(item => (int)item.Group);
        this._spawned = maps.Where(map => map.IsActive).OrderBy(map => map.Number).SelectMany(map => map.MonsterSpawns
            .Where(spawn => spawn.Quantity > 0 && spawn.MonsterDefinition is { IsActive: true })
            .Select(spawn => (Map: map, Monster: spawn.MonsterDefinition!, Level: MonsterLevel(spawn.MonsterDefinition!))))
            .DistinctBy(row => (row.Map.Number, row.Monster.Number)).ToArray();
        this._mixes = this._spawned.GroupBy(row => row.Monster.Number).OrderBy(group => group.Key).SelectMany(group =>
        {
            var npc = group.First().Monster;
            var locations = group.Select(row => Name(row.Map.Name)).Distinct().Take(MaximumDetails).ToArray();
            return npc.ItemCraftings.OrderBy(mix => mix.Number).Select(mix => new Recipe(npc, locations, mix));
        }).ToArray();
        var boxes = boxesEnabled ? this._items.SelectMany(box => box.DropItems.Where(HasConfiguredChance)
            .SelectMany(pool => pool.PossibleItems.Where(item => item.IsActive).Select(item => new BoxInput((item.Group, item.Number), box, pool.SourceItemLevel)))) : [];
        this._boxInputs = boxes.DistinctBy(row => (row.Target, row.Box.Group, row.Box.Number, row.Level)).ToLookup(row => row.Target);
    }

    /// <summary>Reads an item list and a mix list alongside the requested current definitions.</summary>
    /// <param name="group">An optional active group.</param>
    /// <param name="page">The item page.</param>
    /// <param name="number">An optional exact item number within the group.</param>
    /// <param name="mixPage">The mix page.</param>
    /// <param name="mixIndex">An optional exact recipe index.</param>
    /// <returns>The catalogue response or null for an unknown requested definition.</returns>
    public object? Read(int? group, int page, int? number, int mixPage, int? mixIndex)
    {
        var groups = this._groups.Select(rows => rows.Key).ToArray();
        if (group.HasValue && !this._groups.Contains(group.Value))
        {
            return null;
        }

        var selectedGroup = group ?? (groups.Length > 0 ? groups[0] : (int?)null);
        var items = selectedGroup is { } key ? this._groups[key].ToArray() : [];
        var itemIndex = number is { } requestedNumber ? Array.FindIndex(items, item => item.Number == requestedNumber) : -1;
        if ((number.HasValue && itemIndex < 0) || (mixIndex is { } index && (index < 0 || index >= this._mixes.Length)))
        {
            return null;
        }

        var itemPage = itemIndex >= 0 ? (itemIndex / PageSize) + 1 : ClampPage(page, items.Length);
        var currentMixPage = mixIndex is { } requestedMix ? (requestedMix / PageSize) + 1 : ClampPage(mixPage, this._mixes.Length);
        var itemOffset = (itemPage - 1) * PageSize;
        var mixOffset = (currentMixPage - 1) * PageSize;
        var item = items.ElementAtOrDefault(itemIndex >= 0 ? itemIndex : itemOffset);
        var selectedMix = mixIndex ?? mixOffset;
        var recipe = this._mixes.ElementAtOrDefault(selectedMix);
        return new
        {
            groups,
            group = selectedGroup,
            itemPage,
            itemPages = PageCount(items.Length),
            items = items.Skip(itemOffset).Take(PageSize).Select(CatalogueItem).ToArray(),
            item = item is null ? null : this.Item(item),
            boxesEnabled = this._boxesEnabled,
            mixPage = currentMixPage,
            mixPages = PageCount(this._mixes.Length),
            mixes = this._mixes.Skip(mixOffset).Take(PageSize).Select((row, offset) => new { index = mixOffset + offset, name = Name(row.Definition.Name), npc = Name(row.Npc.Designation) }).ToArray(),
            mix = recipe is null ? null : Mix(recipe, selectedMix),
        };
    }

    /// <summary>Limits public configured labels.</summary>
    /// <param name="value">The configured text.</param>
    /// <returns>The trimmed label, or null if there is no text.</returns>
    internal static string? Text(string? value)
    {
        var label = value?.Trim();
        return label is { Length: > 0 } ? label[..Math.Min(120, label.Length)] : null;
    }

    private static int PageCount(int total) => Math.Max(1, (total + PageSize - 1) / PageSize);

    private static int ClampPage(int page, int total) => Math.Clamp(page, 1, PageCount(total));

    private static bool HasConfiguredChance(DropItemGroup group) => double.IsFinite(group.Chance) && group.Chance > 0;

    private static bool IsItemInPool(DropItemGroup group, ItemDefinition item)
        => HasConfiguredChance(group) && group.PossibleItems.Any(candidate => candidate.Group == item.Group && candidate.Number == item.Number);

    private static int? MonsterLevel(MonsterDefinition monster)
        => monster.Attributes?.FirstOrDefault(attribute => attribute.AttributeDefinition == Stats.Level)?.Value is { } level && float.IsFinite(level) ? (int)level : null;

    private static object CatalogueItem(ItemDefinition item) => new { item.Number, name = Name(item.Name) };

    private object Item(ItemDefinition item)
    {
        var sources = this._spawned.Where(source => source.Monster.NumberOfMaximumItemDrops > 0
                && source.Monster.ObjectKind is NpcObjectKind.Monster or NpcObjectKind.Destructible)
            .Select(source => new
            {
                map = Name(source.Map.Name), monster = Name(source.Monster.Designation), source.Level,
                monsterPool = source.Monster.DropItemGroups.Any(group => IsItemInPool(group, item)),
                mapPool = source.Monster.ObjectKind != NpcObjectKind.Destructible && source.Level is { } level && DefaultDropGenerator.CanDropAtMonsterLevel(item, level)
                    && source.Map.DropItemGroups.Any(group => IsItemInPool(group, item) && DefaultDropGenerator.IsGroupRelevant(source.Monster, group)),
            }).Where(source => source.monsterPool || source.mapPool).Take(MaximumSources + 1).ToArray();
        var worldPools = this._configuration.DropItemGroups.Where(group => IsItemInPool(group, item)).Take(MaximumDetails)
            .Select(group => new { name = Name(group.Description), group.MinimumMonsterLevel, group.MaximumMonsterLevel }).ToArray();
        return new
        {
            item.Number,
            name = Name(item.Name),
            slot = item.ItemSlot is { } slot ? Name(slot.Description) : null,
            item.MaximumItemLevel,
            item.DropsFromMonsters,
            minimumMonsterLevel = item.DropLevel,
            item.MaximumDropLevel,
            classes = item.QualifiedCharacters.Where(character => character.IsActive).Take(MaximumDetails).Select(character => Name(character.Name)).ToArray(),
            requirements = item.Requirements.Where(requirement => Text(requirement.Attribute?.Designation) is not null).Take(MaximumDetails)
                .Select(requirement => new { attribute = Text(requirement.Attribute!.Designation), value = requirement.MinimumValue, requirement.MinimumItemLevel }).ToArray(),
            sources = sources.Take(MaximumSources).ToArray(),
            sourcesTruncated = sources.Length > MaximumSources,
            worldPools,
            contents = this._boxesEnabled ? Contents(item) : [],
            foundInBoxes = this._boxInputs[(item.Group, item.Number)].Take(MaximumDetails)
                .Select(source => new { name = Name(source.Box.Name), source.Level }).ToArray(),
        };
    }

    private static object[] Contents(ItemDefinition item) => item.DropItems.GroupBy(pool => pool.SourceItemLevel).OrderBy(group => group.Key)
        .Take(MaximumDetails).Select(group => (object)new
        {
            sourceLevel = group.Key,
            requiredCharacterLevel = group.Max(pool => pool.RequiredCharacterLevel),
            pools = group.Where(HasConfiguredChance).Take(MaximumDetails).Select(pool => new
            {
                kind = pool.ItemType.ToString(),
                money = pool.ItemType == SpecialItemType.Money ? (int?)pool.MoneyAmount : null,
                pool.MinimumLevel,
                pool.MaximumLevel,
                items = pool.PossibleItems.Where(definition => definition.IsActive).Take(MaximumDetails).Select(definition => Name(definition.Name)).ToArray(),
            }).ToArray(),
        }).ToArray();

    private static object Mix(Recipe recipe, int index)
    {
        var supported = WebsiteCraftingQuote.UsesSettings(recipe.Definition);
        var settings = supported ? recipe.Definition.SimpleCraftingSettings : null;
        return new
        {
            index,
            name = Name(recipe.Definition.Name),
            npc = Name(recipe.Npc.Designation),
            recipe.Maps,
            settings = settings is null ? null : new
            {
                basePercent = settings.NpcPriceDivisor > 0 ? null : (int?)settings.SuccessPercent,
                maximumPercent = settings.MaximumSuccessPercent > 0 ? Math.Min((int)settings.MaximumSuccessPercent, FullPercentage) : FullPercentage,
                npcPriceDivisor = settings.NpcPriceDivisor > 0 ? (int?)settings.NpcPriceDivisor : null,
                fixedZen = settings.Money,
                zenPerFinalPercentage = settings.MoneyPerFinalSuccessPercentage,
                ingredients = settings.RequiredItems.Take(MaximumDetails).Select(material => new
                {
                    items = material.PossibleItems.Where(item => item.IsActive).Take(MaximumDetails).Select(item => Name(item.Name)).ToArray(),
                    acceptsAnyItem = material.PossibleItems.Count == 0,
                    material.MinimumAmount,
                    material.MaximumAmount,
                    material.MinimumItemLevel,
                    material.MaximumItemLevel,
                    material.NpcPriceDivisor,
                    material.AddPercentage,
                    material.Reference,
                    options = material.RequiredItemOptions.Take(MaximumDetails).Select(option => Name(option.Name)).ToArray(),
                }).ToArray(),
                results = settings.ResultItems.Where(result => result.ItemDefinition is { IsActive: true } || result.Reference > 0)
                    .Take(MaximumDetails).Select(result => new
                    {
                        name = result.ItemDefinition is { IsActive: true } item ? Name(item.Name) : null,
                        result.RandomMinimumLevel,
                        result.RandomMaximumLevel,
                        result.Reference,
                        result.AddLevel,
                    }).ToArray(),
            },
        };
    }

    private static PublicName Name(LocalizedString name) => new(Text(name.GetTranslation(English)), Text(name.GetTranslation(Vietnamese)));

    private sealed record PublicName(string? En, string? Vi);

    private sealed record BoxInput((byte Group, short Number) Target, ItemDefinition Box, byte Level);

    private sealed record Recipe(MonsterDefinition Npc, PublicName[] Maps, ItemCrafting Definition);
}
