// <copyright file="ChaosCardCrafting.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlayerActions.Craftings;

using HNMO.DataModel.Configuration.ItemCrafting;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.PlayerActions.Items;
using HNMO.GameLogic.Views.NPC;

/// <summary>
/// Chaos Card Master combination handler (<c>MixType = 40</c>) implementing the weighted
/// 11-section Chaos Card reward table from the Season 6 / Season 9 server suites.
/// </summary>
public class ChaosCardCrafting : SimpleItemCraftingHandler
{
    /// <summary>
    /// Gets the Chaos Card mix identifier matching the client mix ID.
    /// </summary>
    public const byte MixNumber = 40;

    private const int RateDenominator = 1_000_000;

    private static readonly int[] OptionLevelWeights = [500_000, 300_000, 150_000, 50_000];
    private static readonly int[] ExcOptionBitWeights = [250_000, 150_000, 150_000, 150_000, 150_000, 150_000];

    /// <summary>
    /// Weighted sections (<c>DropRate</c>, <c>SkillRate</c>, <c>LuckRate</c>, <c>OptRate</c>, <c>ExcRate</c> per million)
    /// and their item entries matching the server suites' Chaos Card tables.
    /// </summary>
    public static readonly IReadOnlyList<ChaosCardSection> Sections = CreateSections();

    private readonly SimpleCraftingSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChaosCardCrafting"/> class.
    /// </summary>
    /// <param name="settings">The crafting settings.</param>
    public ChaosCardCrafting(SimpleCraftingSettings settings)
        : base(settings)
    {
        this._settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public override CraftingResult? TryGetRequiredItems(Player player, out IList<CraftingRequiredItemLink> items, out byte successRate)
    {
        var result = base.TryGetRequiredItems(player, out items, out successRate);
        if (result != default)
        {
            return result;
        }

        return items.Sum(link => link.Items.Count()) == 1
            ? null
            : CraftingResult.IncorrectMixItems;
    }

    /// <inheritdoc />
    protected override async ValueTask<List<Item>> CreateOrModifyResultItemsAsync(
        IList<CraftingRequiredItemLink> requiredItems,
        Player player,
        byte socketSlot,
        byte successRate)
    {
        var configuredLookup = this._settings.ResultItems
            .Where(r => r.ItemDefinition is not null)
            .ToDictionary(
                r => (r.ItemDefinition!.Group, r.ItemDefinition.Number),
                r => r.ItemDefinition!);

        var itemLookup = configuredLookup.Count > 0
            ? configuredLookup
            : player.GameContext.Configuration.Items.ToDictionary(i => (i.Group, i.Number), i => i);

        var eligibleSections = Sections
            .Select(section => (
                Section: section,
                Entries: section.Entries.Where(e => itemLookup.ContainsKey((e.Group, e.Number))).ToList()))
            .Where(pair => pair.Entries.Count > 0)
            .ToList();

        if (eligibleSections.Count == 0)
        {
            return [];
        }

        var chosen = eligibleSections.SelectWeightedRandom(eligibleSections.Select(p => p.Section.DropWeight).ToArray())!;
        var entry = chosen.Entries[Rand.NextInt(0, chosen.Entries.Count)];
        var definition = itemLookup[(entry.Group, entry.Number)];

        var item = BuildRewardItem(player, definition, chosen.Section, entry);
        await player.TemporaryStorage!.AddItemAsync(item).ConfigureAwait(false);
        return [item];
    }

    private static Item BuildRewardItem(Player player, ItemDefinition definition, ChaosCardSection section, ChaosCardEntry entry)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = definition;
        item.Level = entry.MaxLevel > entry.MinLevel
            ? (byte)Rand.NextInt(entry.MinLevel, entry.MaxLevel + 1)
            : entry.MinLevel;

        if (entry.AllowSkill && definition.Skill is not null && RollMillion(section.SkillRate))
        {
            item.HasSkill = true;
        }

        if (entry.AllowLuck && RollMillion(section.LuckRate))
        {
            TryAddSingleOption(player, item, ItemOptionTypes.Luck, level: 0);
        }

        if (entry.AllowOption && RollMillion(section.OptionRate))
        {
            var optionLevel = new[] { 0, 1, 2, 3 }.SelectWeightedRandom(OptionLevelWeights);
            if (optionLevel > 0)
            {
                TryAddSingleOption(player, item, ItemOptionTypes.Option, (byte)optionLevel);
            }
        }

        if (entry.MaxExcellentOptions > 0 && RollMillion(section.ExcellentRate))
        {
            ApplyExcellentOptions(player, item, entry.MaxExcellentOptions);
        }

        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        return item;
    }

    private static bool RollMillion(int rate) => rate >= RateDenominator || (rate > 0 && Rand.NextInt(0, RateDenominator) < rate);

    private static void TryAddSingleOption(Player player, Item item, ItemOptionType optionType, byte level)
    {
        var option = item.Definition!.PossibleItemOptions
            .SelectMany(o => o.PossibleOptions)
            .FirstOrDefault(o => o.OptionType == optionType && (!o.IsFixedOption() || optionType == ItemOptionTypes.Luck));
        if (option is null)
        {
            return;
        }

        var link = player.PersistenceContext.CreateNew<ItemOptionLink>();
        link.ItemOption = option;
        link.Level = level;
        item.ItemOptions.Add(link);
    }

    private static void ApplyExcellentOptions(Player player, Item item, byte maxOptions)
    {
        var excellentGroup = item.Definition!.PossibleItemOptions
            .FirstOrDefault(o => o.PossibleOptions.Any(p => p.OptionType == ItemOptionTypes.Excellent));
        if (excellentGroup is null)
        {
            return;
        }

        var candidates = excellentGroup.PossibleOptions
            .Where(o => o.OptionType == ItemOptionTypes.Excellent)
            .OrderBy(o => o.Number)
            .ToList();
        var targetCount = Math.Min(maxOptions, Math.Min(excellentGroup.MaximumOptionsPerItem, candidates.Count));
        if (targetCount <= 0)
        {
            return;
        }

        var desiredCount = Rand.NextInt(1, targetCount + 1);
        for (int i = 0; i < desiredCount && candidates.Count > 0; i++)
        {
            var weights = candidates
                .Select(c => c.Number >= 1 && c.Number <= ExcOptionBitWeights.Length ? ExcOptionBitWeights[c.Number - 1] : 150_000)
                .ToArray();
            var picked = candidates.SelectWeightedRandom(weights)!;
            candidates.Remove(picked);

            var link = player.PersistenceContext.CreateNew<ItemOptionLink>();
            link.ItemOption = picked;
            item.ItemOptions.Add(link);
        }

        if (item.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent))
        {
            // Excellent rewards are always level +0 and always carry weapon skill when available.
            item.Level = 0;
            if (item.Definition.Skill is not null)
            {
                item.HasSkill = true;
            }
        }
    }

    private static List<ChaosCardSection> CreateSections() =>
    [
        new(160_000, 1_000_000, 350_000, 1_000_000, 600_000,
        [
            new(0, 0, 0, 0, false, true, true, 2),
            .. WeaponRange((0, 16), (0, 17), (0, 18), (0, 31), (2, 10), (2, 11), (4, 16), (4, 17), (4, 19), (4, 6), (2, 6)),
            .. StaffRange((5, 7), (5, 8), (5, 9)),
        ]),
        new(100_000, 1_000_000, 500_000, 1_000_000, 1_000_000,
        [
            new(0, 24, 5, 7, true, true, true, 0),
            new(0, 25, 5, 7, true, true, true, 0),
            new(2, 15, 5, 7, true, true, true, 0),
            new(4, 22, 5, 7, true, true, true, 0),
            new(5, 13, 5, 7, false, true, true, 0),
        ]),
        new(30_000, 1_000_000, 500_000, 1_000_000, 1_000_000,
        [
            new(0, 3, 0, 0, true, true, true, 2),
            new(1, 3, 0, 0, true, true, true, 2),
            new(2, 4, 0, 0, true, true, true, 2),
            new(2, 5, 0, 0, true, true, true, 2),
            new(2, 7, 0, 0, true, true, true, 2),
            new(2, 8, 0, 0, true, true, true, 2),
            new(2, 9, 0, 0, true, true, true, 2),
            new(3, 9, 0, 0, true, true, true, 2),
            new(4, 13, 0, 0, true, true, true, 2),
            new(4, 14, 0, 0, true, true, true, 2),
            new(5, 6, 0, 0, false, true, true, 2),
        ]),
        new(160_000, 0, 350_000, 1_000_000, 600_000,
        [
            new(6, 3, 0, 0, false, true, true, 2),
            new(6, 5, 0, 0, false, true, true, 2),
            new(6, 8, 0, 0, true, true, true, 2),
            new(6, 9, 0, 0, true, true, true, 2),
            new(6, 11, 0, 0, true, true, true, 2),
            new(6, 12, 0, 0, true, true, true, 2),
            new(6, 13, 0, 0, true, true, true, 2),
            new(6, 14, 0, 0, false, true, true, 2),
            new(6, 15, 0, 0, false, true, true, 2),
            new(6, 16, 0, 0, true, true, true, 2),
        ]),
        new(100_000, 0, 500_000, 1_000_000, 1_000_000,
        [
            .. ArmorSets(0, 0, 2, (15, false), (16, true), (17, true), (18, true), (19, true), (20, false), (27, true), (34, true), (35, true), (36, true), (37, false), (38, true)),
            .. ArmorSets(5, 7, 0, (28, false), (21, true), (22, true), (23, false), (24, true)),
        ]),
        new(120_000, 0, 0, 1_000_000, 0, ArmorSets(0, 0, 2, (10, true), (11, true), (12, true), (13, true), (14, true), (25, true), (26, true))),
        new(100_000, 0, 0, 0, 0,
        [
            new(13, 8, 0, 0, false, false, true, 2),
            new(13, 9, 0, 0, false, false, true, 2),
            new(13, 21, 0, 0, false, false, true, 2),
            new(13, 22, 0, 0, false, false, true, 2),
            new(13, 23, 0, 0, false, false, true, 2),
            new(13, 24, 0, 0, false, false, true, 2),
            new(13, 12, 0, 0, false, false, true, 2),
            new(13, 13, 0, 0, false, false, true, 2),
            new(13, 25, 0, 0, false, false, true, 2),
            new(13, 26, 0, 0, false, false, true, 2),
            new(13, 27, 0, 0, false, false, true, 2),
            new(13, 28, 0, 0, false, false, true, 2),
        ]),
        new(157_000, 0, 0, 0, 0, [new(14, 13, 0, 0, false, false, false, 0), new(14, 16, 0, 0, false, false, false, 0), new(14, 42, 0, 0, false, false, false, 0)]),
        new(52_150, 0, 0, 0, 0, [new(12, 15, 0, 0, false, false, false, 0), new(14, 14, 0, 0, false, false, false, 0), new(14, 22, 0, 0, false, false, false, 0), new(14, 41, 0, 0, false, false, false, 0), new(14, 43, 0, 0, false, false, false, 0), new(14, 44, 0, 0, false, false, false, 0)]),
        new(16_600, 0, 0, 0, 0, [new(12, 30, 0, 0, false, false, false, 0), new(12, 31, 0, 0, false, false, false, 0)]),
        new(2_000, 0, 0, 0, 0, [new(13, 4, 0, 0, false, false, false, 0), new(13, 5, 0, 0, false, false, false, 0), new(13, 37, 0, 0, false, false, false, 0)]),
    ];

    private static IEnumerable<ChaosCardEntry> WeaponRange(params (byte Group, short Number)[] items)
    {
        foreach (var (group, number) in items)
        {
            yield return new(group, number, 0, 0, true, true, true, 2);
            yield return new(group, number, 5, 7, true, true, true, 0);
        }
    }

    private static IEnumerable<ChaosCardEntry> StaffRange(params (byte Group, short Number)[] items)
    {
        foreach (var (group, number) in items)
        {
            yield return new(group, number, 0, 0, false, true, true, 2);
            yield return new(group, number, 5, 7, false, true, true, 0);
        }
    }

    private static List<ChaosCardEntry> ArmorSets(byte minLevel, byte maxLevel, byte exc, params (short SetIndex, bool HasHelm)[] sets)
    {
        var result = new List<ChaosCardEntry>();
        foreach (var (setIndex, hasHelm) in sets)
        {
            if (hasHelm)
            {
                result.Add(new(7, setIndex, minLevel, maxLevel, false, true, true, exc));
            }

            result.Add(new(8, setIndex, minLevel, maxLevel, false, true, true, exc));
            result.Add(new(9, setIndex, minLevel, maxLevel, false, true, true, exc));
            result.Add(new(10, setIndex, minLevel, maxLevel, false, true, true, exc));
            result.Add(new(11, setIndex, minLevel, maxLevel, false, true, true, exc));
        }

        return result;
    }

    /// <summary>
    /// Defines one weighted Chaos Card section.
    /// </summary>
    public sealed record ChaosCardSection(
        int DropWeight,
        int SkillRate,
        int LuckRate,
        int OptionRate,
        int ExcellentRate,
        IReadOnlyList<ChaosCardEntry> Entries);

    /// <summary>
    /// Defines one reward candidate entry inside a Chaos Card section.
    /// </summary>
    public sealed record ChaosCardEntry(
        byte Group,
        short Number,
        byte MinLevel,
        byte MaxLevel,
        bool AllowSkill,
        bool AllowLuck,
        bool AllowOption,
        byte MaxExcellentOptions);
}
