// <copyright file="ItemDefinitionStateStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ItemDefinitions;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.Interfaces;
using HNMO.Persistence;

/// <summary>
/// Persists client-side item metadata (Vietnamese translation, glow color, tooltip text, model link) in <see cref="HnmoStateStore"/>
/// and synchronizes shared properties with <see cref="ItemDefinition"/>.
/// </summary>
public static class ItemDefinitionStateStore
{
    /// <summary>
    /// The <see cref="HnmoStateStore"/> section key for item client metadata overrides.
    /// </summary>
    public const string SectionName = "itemDefinitionsMeta";

    /// <summary>
    /// The default shared state file, relative to the game server's working directory.
    /// </summary>
    public const string DefaultFilePath = "../save/" + HnmoStateStore.StateFileName;

    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private static readonly (AttributeDefinition Primary, AttributeDefinition? ValueStat, Func<ItemClientMetadata, int> Getter, Action<ItemClientMetadata, int> Setter)[] RequirementBindings =
    [
        (Stats.Level, null, m => m.LevelRequirement, (m, v) => m.LevelRequirement = v),
        (Stats.TotalStrength, Stats.TotalStrengthRequirementValue, m => m.StrengthRequirement, (m, v) => m.StrengthRequirement = v),
        (Stats.TotalAgility, Stats.TotalAgilityRequirementValue, m => m.AgilityRequirement, (m, v) => m.AgilityRequirement = v),
        (Stats.TotalVitality, Stats.TotalVitalityRequirementValue, m => m.VitalityRequirement, (m, v) => m.VitalityRequirement = v),
        (Stats.TotalEnergy, Stats.TotalEnergyRequirementValue, m => m.EnergyRequirement, (m, v) => m.EnergyRequirement = v),
        (Stats.TotalLeadership, Stats.TotalLeadershipRequirementValue, m => m.LeadershipRequirement, (m, v) => m.LeadershipRequirement = v),
    ];

    private static string _filePath = DefaultFilePath;

    /// <summary>
    /// Gets or sets the shared state file path (tests point it to a temporary file).
    /// </summary>
    public static string FilePath
    {
        get => _filePath;
        set => _filePath = string.IsNullOrWhiteSpace(value) ? DefaultFilePath : value;
    }

    /// <summary>
    /// Returns a snapshot of all saved client item metadata overrides.
    /// </summary>
    /// <returns>The ordered list of overrides.</returns>
    public static IReadOnlyList<ItemClientMetadata> GetAll()
    {
        lock (Gate)
        {
            return Sorted(OpenStore().ReadSection<List<ItemClientMetadata>>(SectionName, JsonOptions) ?? []);
        }
    }

    /// <summary>
    /// Saves or updates client metadata for a single item atomically.
    /// </summary>
    /// <param name="metadata">The metadata to store.</param>
    /// <returns>The updated full snapshot.</returns>
    public static IReadOnlyList<ItemClientMetadata> Upsert(ItemClientMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        metadata.GlowColor = ItemClientDataSync.NormalizeGlowColor(metadata.GlowColor);
        metadata.UpdatedAtUtc = DateTime.UtcNow;

        lock (Gate)
        {
            var store = OpenStore();
            var list = store.ReadSection<List<ItemClientMetadata>>(SectionName, JsonOptions) ?? [];
            list.RemoveAll(x => x.Group == metadata.Group && x.Number == metadata.Number);
            list.Add(metadata);
            var sorted = Sorted(list);
            store.WriteSection(SectionName, sorted, JsonOptions);
            return sorted;
        }
    }

    /// <summary>
    /// Removes client metadata override for a single item.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <returns>A value indicating whether an override existed and was removed.</returns>
    public static bool Remove(byte group, short number)
    {
        lock (Gate)
        {
            var store = OpenStore();
            var list = store.ReadSection<List<ItemClientMetadata>>(SectionName, JsonOptions) ?? [];
            var removed = list.RemoveAll(x => x.Group == group && x.Number == number) > 0;
            if (removed)
            {
                store.WriteSection(SectionName, Sorted(list), JsonOptions);
            }

            return removed;
        }
    }

    /// <summary>
    /// Builds an editable <see cref="ItemClientMetadata"/> snapshot from a server <see cref="ItemDefinition"/> and optional existing metadata.
    /// </summary>
    /// <param name="definition">The server item definition.</param>
    /// <param name="existing">Optional existing metadata override.</param>
    /// <returns>The merged metadata model.</returns>
    public static ItemClientMetadata FromDefinition(ItemDefinition definition, ItemClientMetadata? existing = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var viCulture = CultureInfo.GetCultureInfo("vi");
        var nameEn = definition.Name.ValueInNeutralLanguage;
        var nameVi = definition.Name.GetTranslation(viCulture, fallbackToNeutral: false);

        var meta = new ItemClientMetadata
        {
            Group = definition.Group,
            Number = definition.Number,
            NameEn = !string.IsNullOrWhiteSpace(existing?.NameEn) ? existing.NameEn : nameEn ?? string.Empty,
            NameVi = !string.IsNullOrWhiteSpace(existing?.NameVi) ? existing.NameVi : nameVi ?? string.Empty,
            Width = existing is not null ? existing.Width : Math.Max((byte)1, definition.Width),
            Height = existing is not null ? existing.Height : Math.Max((byte)1, definition.Height),
            DropLevel = existing is not null ? existing.DropLevel : definition.DropLevel,
            MaximumItemLevel = existing is not null ? existing.MaximumItemLevel : definition.MaximumItemLevel,
            Durability = existing is not null ? existing.Durability : definition.Durability,
            BuyPrice = existing is not null ? existing.BuyPrice : definition.Value,
            DropsFromMonsters = existing is not null ? existing.DropsFromMonsters : definition.DropsFromMonsters,
            GlowColor = existing?.GlowColor ?? string.Empty,
            TooltipTextEn = existing?.TooltipTextEn ?? string.Empty,
            TooltipTextVi = existing?.TooltipTextVi ?? string.Empty,
            ModelSourceGroup = existing?.ModelSourceGroup,
            ModelSourceNumber = existing?.ModelSourceNumber,
            UpdatedAtUtc = existing?.UpdatedAtUtc ?? DateTime.UtcNow,
        };

        foreach (var (primary, valueStat, _, setter) in RequirementBindings)
        {
            var req = FindRequirement(definition, primary, valueStat);
            if (req is not null)
            {
                setter(meta, req.MinimumValue);
            }
        }

        if (existing is not null)
        {
            if (existing.LevelRequirement > 0)
            {
                meta.LevelRequirement = existing.LevelRequirement;
            }

            if (existing.StrengthRequirement > 0)
            {
                meta.StrengthRequirement = existing.StrengthRequirement;
            }

            if (existing.AgilityRequirement > 0)
            {
                meta.AgilityRequirement = existing.AgilityRequirement;
            }

            if (existing.VitalityRequirement > 0)
            {
                meta.VitalityRequirement = existing.VitalityRequirement;
            }

            if (existing.EnergyRequirement > 0)
            {
                meta.EnergyRequirement = existing.EnergyRequirement;
            }

            if (existing.LeadershipRequirement > 0)
            {
                meta.LeadershipRequirement = existing.LeadershipRequirement;
            }
        }

        return meta;
    }

    /// <summary>
    /// Applies edited <see cref="ItemClientMetadata"/> fields back onto a server <see cref="ItemDefinition"/>.
    /// </summary>
    /// <param name="definition">The target item definition.</param>
    /// <param name="metadata">The metadata values.</param>
    /// <param name="gameConfiguration">The owning game configuration.</param>
    /// <param name="context">The persistence context used to allocate new requirements when needed.</param>
    public static void ApplyToDefinition(
        ItemDefinition definition,
        ItemClientMetadata metadata,
        GameConfiguration gameConfiguration,
        IContext context)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(gameConfiguration);
        ArgumentNullException.ThrowIfNull(context);

        definition.Group = metadata.Group;
        definition.Number = metadata.Number;
        definition.Width = Math.Clamp(metadata.Width, (byte)1, (byte)8);
        definition.Height = Math.Clamp(metadata.Height, (byte)1, (byte)8);
        definition.DropLevel = metadata.DropLevel;
        definition.MaximumItemLevel = Math.Clamp(metadata.MaximumItemLevel, (byte)0, (byte)15);
        definition.Durability = metadata.Durability;
        definition.Value = Math.Max(0, metadata.BuyPrice);
        definition.DropsFromMonsters = metadata.DropsFromMonsters;

        var enCulture = CultureInfo.GetCultureInfo("en");
        var viCulture = CultureInfo.GetCultureInfo("vi");
        var localized = definition.Name.WithTranslation(enCulture, metadata.NameEn.Trim());
        localized = localized.WithTranslation(viCulture, string.IsNullOrWhiteSpace(metadata.NameVi) ? null : metadata.NameVi.Trim());
        definition.Name = localized;

        foreach (var (primary, valueStat, getter, _) in RequirementBindings)
        {
            var desired = Math.Max(0, getter(metadata));
            var existing = FindRequirement(definition, primary, valueStat);
            if (desired == 0)
            {
                if (existing is not null)
                {
                    definition.Requirements.Remove(existing);
                }

                continue;
            }

            if (existing is null)
            {
                existing = context.CreateNew<AttributeRequirement>();
                existing.Attribute = ResolveAttribute(gameConfiguration, primary, valueStat);
                definition.Requirements.Add(existing);
            }

            existing.MinimumValue = desired;
        }
    }

    private static HnmoStateStore OpenStore()
    {
        return HnmoStateStore.Open(_filePath)
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-items-{Guid.NewGuid():N}.json"));
    }

    private static List<ItemClientMetadata> Sorted(IEnumerable<ItemClientMetadata> source) =>
        source.OrderBy(i => i.Group).ThenBy(i => i.Number).ToList();

    private static AttributeDefinition ResolveAttribute(
        GameConfiguration gameConfiguration,
        AttributeDefinition primary,
        AttributeDefinition? valueStat)
    {
        var preferredId = (valueStat ?? primary).Id;
        var persistent = gameConfiguration.Attributes.FirstOrDefault(a => a.Id == preferredId)
            ?? gameConfiguration.Attributes.FirstOrDefault(a => a.Id == primary.Id);
        return persistent ?? valueStat ?? primary;
    }

    private static AttributeRequirement? FindRequirement(
        ItemDefinition definition,
        AttributeDefinition primary,
        AttributeDefinition? valueStat)
    {
        return definition.Requirements.FirstOrDefault(r =>
            r.Attribute?.Id == primary.Id
            || (valueStat is not null && r.Attribute?.Id == valueStat.Id));
    }
}
