// <copyright file="WebsiteHomeLootProjection.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Web.AdminPanel.API;

using System.Globalization;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic;
using HNMO.Interfaces;

/// <summary>Projects reachable field drop pools and the highest configured sources for equipment.</summary>
public static class WebsiteHomeLootProjection
{
    private const int MaximumRows = 32;
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi");

    /// <summary>Reads the selected server's active map spawns, stores and field drop configuration.</summary>
    /// <param name="configuration">The running game configuration.</param>
    /// <param name="maps">The maps assigned to the server.</param>
    /// <param name="availableShopProducts">Products that the active shop can sell and deliver.</param>
    /// <returns>The public summaries without player data or private configuration identifiers.</returns>
    public static object Build(GameConfiguration configuration, IEnumerable<GameMapDefinition> maps, IEnumerable<CashShopProduct> availableShopProducts)
    {
        var generator = new DefaultDropGenerator(configuration, Rand.GetRandomizer());
        var gear = new Dictionary<ItemDefinition, GearSource>();
        var groups = new Dictionary<DropItemGroup, DropSummary>();
        var merchants = new HashSet<MonsterDefinition>();
        foreach (var map in maps.Where(map => map.IsActive))
        {
            var spawned = map.MonsterSpawns.Where(spawn => spawn is { Quantity: > 0, SpawnTrigger: SpawnTrigger.Automatic or SpawnTrigger.Wandering })
                .Select(spawn => spawn.MonsterDefinition).OfType<MonsterDefinition>().Where(monster => monster.IsActive).Distinct();
            foreach (var monster in spawned)
            {
                if (monster.ObjectKind is NpcObjectKind.Monster or NpcObjectKind.Destructible)
                {
                    AddFieldSources(gear, groups, generator.DescribeConfiguredFieldDrops(monster, map), map, monster);
                }
                else if (monster.MerchantStore is { } store && merchants.Add(monster))
                {
                    foreach (var item in store.GetOfferedItems())
                    {
                        AddGear(gear, item.Definition, new(item.Level, "merchant", monster.Designation, map.Name));
                    }
                }
            }
        }

        foreach (var product in availableShopProducts)
        {
            AddGear(gear, product.ItemDefinition, new(product.ItemLevel, "catalog", default, default));
        }

        return Project(gear, groups);
    }

    private static void AddFieldSources(
        Dictionary<ItemDefinition, GearSource> gear,
        Dictionary<DropItemGroup, DropSummary> summaries,
        IEnumerable<ConfiguredMonsterDrop> drops,
        GameMapDefinition map,
        MonsterDefinition monster)
    {
        foreach (var drop in drops)
        {
            var hasItems = false;
            foreach (var (item, level) in drop.Items)
            {
                hasItems = true;
                AddGear(gear, item, new(level, "field", monster.Designation, map.Name));
            }

            if (!hasItems || drop.Group.ItemType == SpecialItemType.Money)
            {
                continue;
            }

            var repeat = summaries.TryGetValue(drop.Group, out var previous);
            summaries[drop.Group] = repeat
                ? new(Math.Min(previous.Minimum, drop.ChancePerRoll), Math.Max(previous.Maximum, drop.ChancePerRoll))
                : new(drop.ChancePerRoll, drop.ChancePerRoll);
        }
    }

    private static void AddGear(Dictionary<ItemDefinition, GearSource> gear, ItemDefinition? item, GearSource source)
    {
        if (item is { IsActive: true, ItemSlot: not null }
            && (!gear.TryGetValue(item, out var previous) || previous.Level < source.Level))
        {
            gear[item] = source;
        }
    }

    private static object Project(Dictionary<ItemDefinition, GearSource> gear, Dictionary<DropItemGroup, DropSummary> groups)
    {
        var highestBySlot = gear.GroupBy(pair => pair.Key.ItemSlot!)
            .SelectMany(slot =>
            {
                var highest = slot.Max(pair => pair.Value.Level);
                return slot.Where(pair => pair.Value.Level == highest);
            })
            .OrderByDescending(pair => pair.Value.Level).ThenByDescending(pair => pair.Key.DropLevel)
            .ThenBy(pair => pair.Key.Group).ThenBy(pair => pair.Key.Number).Take(MaximumRows + 1).ToArray();
        var topGroups = groups.OrderByDescending(pair => pair.Value.Maximum).ThenBy(pair => pair.Key.Description.Value, StringComparer.Ordinal)
            .Take(MaximumRows + 1).ToArray();
        return new
        {
            gear = highestBySlot.Take(MaximumRows).Select(pair => new
            {
                nameEn = pair.Key.Name.GetTranslation(English),
                nameVi = pair.Key.Name.GetTranslation(Vietnamese),
                slotEn = pair.Key.ItemSlot!.Description.GetTranslation(English),
                slotVi = pair.Key.ItemSlot!.Description.GetTranslation(Vietnamese),
                maximumLevel = pair.Value.Level,
                source = pair.Value.Kind,
                sourceEn = pair.Value.Name.GetTranslation(English),
                sourceVi = pair.Value.Name.GetTranslation(Vietnamese),
                mapEn = pair.Value.Map.GetTranslation(English),
                mapVi = pair.Value.Map.GetTranslation(Vietnamese),
            }).ToArray(),
            gearTruncated = highestBySlot.Length > MaximumRows,
            groups = topGroups.Take(MaximumRows).Select(pair => new
            {
                nameEn = pair.Key.Description.GetTranslation(English),
                nameVi = pair.Key.Description.GetTranslation(Vietnamese),
                minimumChance = pair.Value.Minimum,
                maximumChance = pair.Value.Maximum,
            }).ToArray(),
            groupsTruncated = topGroups.Length > MaximumRows,
        };
    }

    private readonly record struct GearSource(byte Level, string Kind, LocalizedString Name, LocalizedString Map);

    private readonly record struct DropSummary(double Minimum, double Maximum);
}
