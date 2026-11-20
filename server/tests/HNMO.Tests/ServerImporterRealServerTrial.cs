// <copyright file="ServerImporterRealServerTrial.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Tests;

using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.ServerImporter;
using HNMO.Persistence.InMemory;
using HNMO.PlugIns;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Imports a real legacy server folder into a fresh Season 6 configuration (in memory) and writes what came out.
/// Explicit: it needs the server files, given by HNMO_IMPORT_TRIAL_FOLDER, and writes to HNMO_IMPORT_TRIAL_REPORT.
/// </summary>
[TestFixture]
[Explicit("Needs a real legacy server folder (HNMO_IMPORT_TRIAL_FOLDER).")]
public class ServerImporterRealServerTrial
{
    /// <summary>Runs the preview and the import of the folder and reports drops, spawns, shops, events and rates.</summary>
    [Test]
    public async Task ImportRealServerFolderAsync()
    {
        var folder = Environment.GetEnvironmentVariable("HNMO_IMPORT_TRIAL_FOLDER");
        var reportPath = Environment.GetEnvironmentVariable("HNMO_IMPORT_TRIAL_REPORT") ?? Path.Combine(Path.GetTempPath(), "hnmo-import-trial.txt");
        Assume.That(folder is not null && Directory.Exists(folder), "HNMO_IMPORT_TRIAL_FOLDER is not set");

        var provider = new InMemoryPersistenceContextProvider();
        await new Persistence.Initialization.VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();

        var spawnsBefore = config.Maps.ToDictionary(m => m, m => m.MonsterSpawns.Count);
        var monsterGroupsBefore = config.Monsters.Sum(m => m.DropItemGroups.Count);
        var mapGroupsBefore = config.Maps.Sum(m => m.DropItemGroups.Count);
        var report = new StringBuilder();

        var options = new LegacyImportOptions { FolderPath = folder!, ImportDatabaseEntities = false };
        var preview = await LegacyServerImportEngine.BuildDryRunPreviewAsync(options, config).ConfigureAwait(false);
        report.AppendLine(CultureInfo.InvariantCulture, $"Folder: {folder}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Version: {preview.DetectedVersion} ({string.Join(" | ", preview.VersionSignals)})");
        foreach (var category in preview.Categories)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"[{category.NameEn}] mapped {category.MappedCount}, skipped {category.SkippedCount}");
            foreach (var detail in category.Details)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"    {detail}");
            }
        }

        report.AppendLine("Monster stat differences (HNMO -> server), first 25:");
        foreach (var stats in preview.MonsterStats.Take(25))
        {
            var monster = config.Monsters.First(m => m.Number == stats.Number);
            float Of(HNMO.AttributeSystem.AttributeDefinition stat) => monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == stat.Id)?.Value ?? 0;
            var parts = new List<string>();
            void Diff(string name, double before, double after)
            {
                if (Math.Abs(before - after) > 0.001)
                {
                    parts.Add(string.Create(CultureInfo.InvariantCulture, $"{name} {before}->{after}"));
                }
            }

            Diff("lvl", Of(HNMO.GameLogic.Attributes.Stats.Level), stats.Level);
            Diff("hp", Of(HNMO.GameLogic.Attributes.Stats.MaximumHealth), stats.Health);
            Diff("dmin", Of(HNMO.GameLogic.Attributes.Stats.MinimumPhysBaseDmg), stats.DamageMin);
            Diff("dmax", Of(HNMO.GameLogic.Attributes.Stats.MaximumPhysBaseDmg), stats.DamageMax);
            Diff("def", Of(HNMO.GameLogic.Attributes.Stats.DefenseBase), stats.Defense);
            Diff("ar", Of(HNMO.GameLogic.Attributes.Stats.AttackRatePvm), stats.AttackRate);
            Diff("dr", Of(HNMO.GameLogic.Attributes.Stats.DefenseRatePvm), stats.DefenseRate);
            Diff("move", monster.MoveRange, stats.MoveRange);
            Diff("atkRange", monster.AttackRange, stats.AttackRange);
            Diff("view", monster.ViewRange, stats.ViewRange);
            Diff("moveMs", monster.MoveDelay.TotalMilliseconds, stats.MoveDelayMilliseconds);
            Diff("atkMs", monster.AttackDelay.TotalMilliseconds, stats.AttackDelayMilliseconds);
            Diff("respawnS", monster.RespawnDelay.TotalSeconds, stats.RespawnSeconds);
            report.AppendLine(CultureInfo.InvariantCulture, $"    {stats.Number} {stats.Name}: {string.Join(", ", parts)}");
        }

        var backups = Path.Combine(Path.GetTempPath(), $"hnmo-import-trial-{Guid.NewGuid():N}");
        var result = await LegacyServerImportEngine.ApplyImportAsync(options, context, config, backups).ConfigureAwait(false);
        report.AppendLine(CultureInfo.InvariantCulture, $"Applied {result.AppliedCount}, skipped {result.SkippedCount}");
        foreach (var line in result.SummaryLines.Take(40))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"    {line}");
        }

        var imported = config.DropItemGroups.Where(g => g.Description.ValueInNeutralLanguage.Contains("Imported Drops", StringComparison.Ordinal)).ToList();
        report.AppendLine(CultureInfo.InvariantCulture, $"Drops: {imported.Count} groups; monster groups {monsterGroupsBefore} -> {config.Monsters.Sum(m => m.DropItemGroups.Count)}; map groups {mapGroupsBefore} -> {config.Maps.Sum(m => m.DropItemGroups.Count)}");
        foreach (var group in imported.OrderByDescending(g => g.Chance).Take(40))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"    {group.Monster?.Designation.ValueInNeutralLanguage ?? "(map)"} {group.ItemType} chance {group.Chance:0.######} items {group.PossibleItems.Count}");
        }

        report.AppendLine("Spawns per map (before -> after):");
        foreach (var (map, before) in spawnsBefore.Where(kv => kv.Key.MonsterSpawns.Count != kv.Value).OrderBy(kv => kv.Key.Number))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"    {map.Number} {map.Name.ValueInNeutralLanguage}: {before} -> {map.MonsterSpawns.Count}");
            foreach (var added in map.MonsterSpawns.Skip(before))
            {
                var near = map.MonsterSpawns.Take(before)
                    .Where(s => s.MonsterDefinition == added.MonsterDefinition)
                    .Select(s => $"({s.X1},{s.Y1})-({s.X2},{s.Y2})x{s.Quantity}")
                    .Take(3);
                report.AppendLine(CultureInfo.InvariantCulture, $"        + {added.MonsterDefinition?.Number} {added.MonsterDefinition?.Designation.ValueInNeutralLanguage} ({added.X1},{added.Y1})-({added.X2},{added.Y2})x{added.Quantity}; map had: {string.Join(" ", near)}");
            }
        }

        report.AppendLine("Shops:");
        foreach (var npc in config.Monsters.Where(m => m.MerchantStore is { Items.Count: > 0 }).OrderBy(m => m.Number))
        {
            var cells = new HashSet<int>();
            var overlaps = 0;
            foreach (var item in npc.MerchantStore!.Items)
            {
                var width = Math.Max(1, (int)(item.Definition?.Width ?? 1));
                var height = Math.Max(1, (int)(item.Definition?.Height ?? 1));
                for (var r = 0; r < height; r++)
                {
                    for (var c = 0; c < width; c++)
                    {
                        var cell = item.ItemSlot + (r * 8) + c;
                        if (!cells.Add(cell) || (item.ItemSlot % 8) + width > 8)
                        {
                            overlaps++;
                        }
                    }
                }
            }

            var withOptions = npc.MerchantStore.Items.Count(i => i.ItemOptions.Count > 0);
            report.AppendLine(CultureInfo.InvariantCulture, $"    {npc.Number} {npc.Designation.ValueInNeutralLanguage}: {npc.MerchantStore.Items.Count} items, {withOptions} with options, {overlaps} overlapping cells");
        }

        var bonus = config.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeof(BonusEventsPlugIn).GUID)?.GetConfiguration<BonusEventsConfiguration>(null);
        report.AppendLine("Bonus events:");
        foreach (var ev in bonus?.Events ?? [])
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"    {ev.Id} map {ev.MapNumber?.ToString(CultureInfo.InvariantCulture) ?? "all"} {ev.StartTimeSydney} {ev.DurationMinutes} min EXP +{ev.ExpBonusPercent}% drop +{ev.ItemDropBonusPercent}%");
        }

        var tuning = config.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeof(RateTuningPlugIn).GUID)?.GetConfiguration<RateTuningConfiguration>(null);
        report.AppendLine(CultureInfo.InvariantCulture, $"Rates: EXP {config.ExperienceRate}, master {config.MasterExperienceRate}, soul {tuning?.SoulSuccessRatePercent}+{tuning?.SoulLuckBonusPercent}, life {tuning?.LifeSuccessRatePercent}, zen {tuning?.ZenDropMultiplierPercent}");
        var random = config.DropItemGroups.FirstOrDefault(g => g.ItemType == SpecialItemType.RandomItem && g.Monster is null && g.PossibleItems.Count == 0);
        var excellent = config.DropItemGroups.FirstOrDefault(g => g.ItemType == SpecialItemType.Excellent && g.Monster is null && g.PossibleItems.Count == 0);
        report.AppendLine(CultureInfo.InvariantCulture, $"Common drop groups: random {random?.Chance}, excellent {excellent?.Chance}");
        foreach (var crafting in config.Monsters.SelectMany(m => m.ItemCraftings).Where(c => c.Number is 3 or 4 or 22 or 23 or 49 or 50).OrderBy(c => c.Number))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"    crafting {crafting.Number}: {crafting.SimpleCraftingSettings?.SuccessPercent}% +{crafting.SimpleCraftingSettings?.SuccessPercentageAdditionForLuck}% luck");
        }

        report.AppendLine(CultureInfo.InvariantCulture, $"Warnings ({preview.UnmappedWarnings.Count}):");
        foreach (var warning in preview.UnmappedWarnings.Where(w => !w.StartsWith("Drop (", StringComparison.Ordinal)).Take(30))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"    {warning}");
        }

        await File.WriteAllTextAsync(reportPath, report.ToString()).ConfigureAwait(false);
        Directory.Delete(backups, recursive: true);
    }
}
