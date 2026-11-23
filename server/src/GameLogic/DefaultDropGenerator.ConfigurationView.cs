// <copyright file="DefaultDropGenerator.ConfigurationView.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic;

using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;

public partial class DefaultDropGenerator
{
    /// <summary>Describes a spawned monster's configured base rolls on its map.</summary>
    /// <remarks>
    /// Run this on a separate analysis instance. The definition pools share the generator's lazy caches.
    /// Player, quest and temporary bonus groups are specific to each kill and are outside this baseline.
    /// </remarks>
    /// <param name="monster">The spawned monster definition.</param>
    /// <param name="map">The active map.</param>
    /// <returns>Groups the generator can select within the monster's base roll count.</returns>
    public IEnumerable<ConfiguredMonsterDrop> DescribeConfiguredFieldDrops(MonsterDefinition monster, GameMapDefinition map)
    {
        if (monster.NumberOfMaximumItemDrops <= 0)
        {
            yield break;
        }

        var ownGroups = monster.DropItemGroups;
        var mapGroups = monster.ObjectKind == NpcObjectKind.Destructible
            ? Enumerable.Empty<DropItemGroup>()
            : map.DropItemGroups.Where(group => IsGroupRelevant(monster, group));
        var groups = ownGroups.Concat(mapGroups).Where(group => double.IsFinite(group.Chance) && group.Chance > 0).ToArray();
        var maximumRolls = monster.NumberOfMaximumItemDrops;
        var guaranteed = groups.Where(group => group.Chance >= 1).Take(maximumRolls).ToArray();
        foreach (var group in guaranteed)
        {
            yield return new(group, 1, this.DescribeResults(monster, group));
        }

        if (guaranteed.Length == maximumRolls)
        {
            yield break;
        }

        var random = groups.Where(group => group.Chance < 1).ToArray();
        var divisor = Math.Max(1, random.Sum(group => group.Chance));
        foreach (var group in random)
        {
            yield return new(group, group.Chance / divisor, this.DescribeResults(monster, group));
        }
    }

    private IEnumerable<(ItemDefinition Item, byte MaximumLevel)> DescribeResults(MonsterDefinition monster, DropItemGroup group)
    {
        var level = (int)monster[Stats.Level];
        var explicitItems = group.PossibleItems.Count > 0;
        var items = this.GetConfiguredCandidates(monster, group, level);
        foreach (var item in items.Where(item => item.IsActive))
        {
            yield return (item, explicitItems
                ? Math.Min(item.MaximumItemLevel, group is ItemDropItemGroup range ? range.MaximumLevel : group.ItemLevel ?? 0)
                : group.ItemType is SpecialItemType.RandomItem or SpecialItemType.SocketItem ? GetItemLevelByMonsterLevel(item, level) : (byte)0);
        }
    }

    private IEnumerable<ItemDefinition> GetConfiguredCandidates(MonsterDefinition monster, DropItemGroup group, int level)
    {
        if (group.ItemType == SpecialItemType.Ancient)
        {
            return this._ancientItems;
        }

        if (group.PossibleItems.Count > 0)
        {
            var items = group.PossibleItems.AsEnumerable();
            if (!monster.DropItemGroups.Contains(group))
            {
                items = items.Where(item => CanDropAtMonsterLevel(item, level));
            }

            return group.ItemType == SpecialItemType.Excellent ? items.Where(item => item.MaximumSockets == 0) : items;
        }

        return group.ItemType switch
        {
            SpecialItemType.Excellent => (this.GetPossibleList(level - this._excellentItemDropLevelDelta) ?? []).Where(item => item.MaximumSockets == 0),
            SpecialItemType.RandomItem => this.GetPossibleList(level) ?? [],
            SpecialItemType.SocketItem => this.GetPossibleList(level, true) ?? [],
            _ => [],
        };
    }
}
