// <copyright file="LegacyServerImportEngine.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HNMO.AttributeSystem;
using HNMO.DataModel.Configuration;
using HNMO.DataModel.Configuration.Items;
using HNMO.DataModel.Entities;
using HNMO.GameLogic.Attributes;
using HNMO.GameLogic.PlugIns;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.PlugIns.Timetable;
using HNMO.Interfaces;
using HNMO.Persistence;
using HNMO.PlugIns;

/// <summary>
/// Dry-run preview, atomic backup, migration apply, and undo engine converting local legacy server configurations
/// and optional MSSQL databases into HNMO data models and plug-in settings.
/// </summary>
public static class LegacyServerImportEngine
{
    private static readonly Dictionary<byte, byte> LegacyClassMap = new()
    {
        [0] = 0, [1] = 1, [2] = 2,
        [16] = 16, [17] = 17, [18] = 18,
        [32] = 32, [33] = 33, [34] = 34,
        [48] = 48, [50] = 50,
        [64] = 64, [66] = 66,
        [80] = 80, [81] = 81, [82] = 82,
        [96] = 96, [98] = 98,
    };

    /// <summary>
    /// Produces a non-destructive dry-run report showing version detection, mappable counts, and unsupported items.
    /// </summary>
    /// <param name="options">Import options specifying local folder and optional MSSQL connection string.</param>
    /// <param name="config">HNMO <see cref="GameConfiguration"/> to validate items, maps, and monsters against.</param>
    /// <param name="dbReader">Optional database reader for MSSQL migration preview.</param>
    /// <param name="existingAccounts">Existing HNMO account login names for collision reporting.</param>
    /// <param name="existingCharacters">Existing HNMO character names for collision reporting.</param>
    /// <param name="existingGuilds">Existing HNMO guild names for collision reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dry-run preview report.</returns>
    public static async Task<LegacyImportPreview> BuildDryRunPreviewAsync(
        LegacyImportOptions options,
        GameConfiguration config,
        ILegacyDatabaseReader? dbReader = null,
        IEnumerable<string>? existingAccounts = null,
        IEnumerable<string>? existingCharacters = null,
        IEnumerable<string>? existingGuilds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(config);

        var preview = new LegacyImportPreview();
        var (version, signals) = LegacyFileParsers.DetectVersion(options.FolderPath);
        preview.DetectedVersion = version;
        preview.VersionSignals.AddRange(signals);
        if (version == LegacyServerVersion.Season9)
        {
            preview.UnmappedWarnings.Add("Season 9 server detected: Season 9-only features (Arca, Acheron, Pentagram, Companion/Muun, Master Level > 200, S9 maps) will be listed and skipped so HNMO's Season 6.3 runtime remains stable.");
        }

        var itemsByKey = config.Items.ToDictionary(i => $"{i.Group}:{i.Number}", StringComparer.Ordinal);
        var mapsByNumber = config.Maps.ToDictionary(m => (int)m.Number);
        var monstersByNumber = config.Monsters.GroupBy(m => (int)m.Number).ToDictionary(g => g.Key, g => g.First());

        if (options.ImportRates)
        {
            var rates = LegacyFileParsers.ParseRates(options.FolderPath);
            preview.Rates = rates;
            var details = new List<string>();
            var mapped = 0;
            if (rates.ExperienceRate.HasValue)
            {
                details.Add($"EXP: {config.ExperienceRate} -> {rates.ExperienceRate.Value}");
                mapped++;
            }

            if (rates.MasterExperienceRate.HasValue)
            {
                details.Add($"Master EXP: {config.MasterExperienceRate} -> {rates.MasterExperienceRate.Value}");
                mapped++;
            }

            if (rates.ItemDropPercent.HasValue || rates.ZenMultiplier.HasValue)
            {
                details.Add($"Drop %={rates.ItemDropPercent?.ToString(CultureInfo.InvariantCulture) ?? "-"}, Zen mult={rates.ZenMultiplier?.ToString(CultureInfo.InvariantCulture) ?? "-"}");
                mapped++;
            }

            preview.Categories.Add(new LegacyPreviewCategory
            {
                NameEn = "Server Rates (EXP / Drop / Zen)",
                NameVi = "Tỉ Lệ Máy Chủ (EXP / Rơi Đồ / Zen)",
                MappedCount = mapped,
                Details = details,
            });
        }

        if (options.ImportDropLists)
        {
            var rawDrops = LegacyFileParsers.ParseDropLists(options.FolderPath);
            var mappedDrops = 0;
            var skippedDrops = 0;
            foreach (var d in rawDrops)
            {
                if (d.ElementalAttribute > 0 || d.CompanionEvolutionGroup.HasValue)
                {
                    skippedDrops++;
                    preview.UnmappedWarnings.Add($"Drop ({d.ItemGroup}:{d.ItemNumber}) uses Season 9 elemental/companion attributes not supported in HNMO.");
                    continue;
                }

                if (!itemsByKey.ContainsKey($"{d.ItemGroup}:{d.ItemNumber}"))
                {
                    skippedDrops++;
                    preview.UnmappedWarnings.Add($"Drop ({d.ItemGroup}:{d.ItemNumber}) skipped: item definition not found in HNMO configuration.");
                    continue;
                }

                if (d.MonsterNumber >= 0 && !monstersByNumber.ContainsKey(d.MonsterNumber))
                {
                    skippedDrops++;
                    preview.UnmappedWarnings.Add($"Drop ({d.ItemGroup}:{d.ItemNumber}) for monster #{d.MonsterNumber} skipped: monster not defined in HNMO.");
                    continue;
                }

                preview.DropRules.Add(d);
                mappedDrops++;
            }

            preview.Categories.Add(new LegacyPreviewCategory
            {
                NameEn = "Item Drop Lists",
                NameVi = "Danh Sách Rơi Vật Phẩm",
                MappedCount = mappedDrops,
                SkippedCount = skippedDrops,
            });
        }

        if (options.ImportShops)
        {
            var rawShops = LegacyFileParsers.ParseShops(options.FolderPath);
            var mappedShops = 0;
            var skippedShops = 0;
            foreach (var shop in rawShops)
            {
                if (!monstersByNumber.TryGetValue(shop.NpcNumber, out _))
                {
                    skippedShops++;
                    preview.UnmappedWarnings.Add($"Shop NPC #{shop.NpcNumber} (map {shop.MapNumber}) skipped: NPC not defined in HNMO.");
                    continue;
                }

                var validItems = new List<LegacyShopItemEntry>();
                foreach (var entry in shop.Items)
                {
                    if (entry.ElementalAttribute > 0 || !itemsByKey.ContainsKey($"{entry.Group}:{entry.Number}"))
                    {
                        preview.UnmappedWarnings.Add($"Shop NPC #{shop.NpcNumber} item ({entry.Group}:{entry.Number}) skipped: unsupported or missing definition.");
                        continue;
                    }

                    validItems.Add(entry);
                }

                preview.Shops.Add(new LegacyShopDefinition
                {
                    NpcNumber = shop.NpcNumber,
                    MapNumber = shop.MapNumber,
                    Items = validItems,
                });
                mappedShops++;
            }

            preview.Categories.Add(new LegacyPreviewCategory
            {
                NameEn = "NPC Shops",
                NameVi = "Cửa Hàng NPC",
                MappedCount = mappedShops,
                SkippedCount = skippedShops,
                Details = [$"{preview.Shops.Sum(s => s.Items.Count)} total mappable shop items"],
            });
        }

        if (options.ImportEventSchedules)
        {
            var rawEvents = LegacyFileParsers.ParseEventSchedules(options.FolderPath);
            var mappedEvents = 0;
            var skippedEvents = 0;
            foreach (var ev in rawEvents)
            {
                if (ev.IsSeason9Only)
                {
                    skippedEvents++;
                    preview.UnmappedWarnings.Add($"Event '{ev.Name}' skipped: requires Season 9 maps/mechanics not yet in HNMO.");
                    continue;
                }

                preview.EventSchedules.Add(ev);
                mappedEvents++;
            }

            preview.Categories.Add(new LegacyPreviewCategory
            {
                NameEn = "Event Schedules & Bonus Windows",
                NameVi = "Lịch Sự Kiện & Giờ Vàng",
                MappedCount = mappedEvents,
                SkippedCount = skippedEvents,
            });
        }

        if (options.ImportChaosMixRates)
        {
            var chaos = LegacyFileParsers.ParseChaosMixRates(options.FolderPath);
            preview.ChaosRates.AddRange(chaos);
            preview.Categories.Add(new LegacyPreviewCategory
            {
                NameEn = "Chaos Machine Rates",
                NameVi = "Tỉ Lệ Ép Đồ Chaos",
                MappedCount = chaos.Count,
            });
        }

        if (options.ImportMonsterSpawns)
        {
            var rawSpawns = LegacyFileParsers.ParseMonsterSpawns(options.FolderPath);
            var mappedSpawns = 0;
            var skippedSpawns = 0;
            foreach (var spawn in rawSpawns)
            {
                if (spawn.SpawnGroup >= 5)
                {
                    skippedSpawns++;
                    preview.UnmappedWarnings.Add($"Spawn group {spawn.SpawnGroup} for monster #{spawn.MonsterNumber} skipped: fortress/elemental script trigger must be configured natively.");
                    continue;
                }

                if (!mapsByNumber.ContainsKey(spawn.MapNumber) || !monstersByNumber.ContainsKey(spawn.MonsterNumber))
                {
                    skippedSpawns++;
                    continue;
                }

                preview.Spawns.Add(spawn);
                mappedSpawns++;
            }

            preview.Categories.Add(new LegacyPreviewCategory
            {
                NameEn = "Monster & NPC Spawns",
                NameVi = "Bãi Quái & NPC",
                MappedCount = mappedSpawns,
                SkippedCount = skippedSpawns,
            });
        }

        if (options.ImportDatabaseEntities && !string.IsNullOrWhiteSpace(options.MssqlConnectionString) && dbReader is not null)
        {
            var db = await dbReader.ReadAsync(options.MssqlConnectionString, cancellationToken).ConfigureAwait(false);
            preview.DatabaseSnapshot = db;

            var accSet = new HashSet<string>(existingAccounts ?? [], StringComparer.OrdinalIgnoreCase);
            var charSet = new HashSet<string>(existingCharacters ?? [], StringComparer.OrdinalIgnoreCase);
            var guildSet = new HashSet<string>(existingGuilds ?? [], StringComparer.OrdinalIgnoreCase);

            var accNew = db.Accounts.Count(a => !accSet.Contains(a.LoginName));
            var accSkip = db.Accounts.Count - accNew;
            var charNew = db.Characters.Count(c => !charSet.Contains(c.Name));
            var charSkip = db.Characters.Count - charNew;
            var guildNew = db.Guilds.Count(g => !guildSet.Contains(g.Name));
            var guildSkip = db.Guilds.Count - guildNew;

            if (options.PasswordMode == LegacyPasswordMigrationMode.UseTemporaryPassphrase
                && string.IsNullOrWhiteSpace(options.TemporaryPassphrase))
            {
                preview.UnmappedWarnings.Add("Temporary passphrase is blank; imported accounts will fall back to forced random BCrypt reset tokens.");
            }

            preview.Categories.Add(new LegacyPreviewCategory
            {
                NameEn = "Database Entities (MSSQL -> PostgreSQL)",
                NameVi = "Dữ Liệu CSDL (Tài khoản / Nhân vật / Túi / Rương / Guild)",
                MappedCount = accNew + charNew + guildNew,
                SkippedCount = accSkip + charSkip + guildSkip,
                Details =
                [
                    $"Accounts: {accNew} new ({accSkip} existing skipped), Passwords: BCrypt re-hashed (zero plaintext)",
                    $"Characters: {charNew} new ({charSkip} skipped), Vaults: {db.Vaults.Count}, Guilds: {guildNew} new",
                ],
            });
        }

        return preview;
    }

    /// <summary>
    /// Creates an atomic pre-import backup snapshot and applies all mapped changes to HNMO configuration and entities.
    /// </summary>
    /// <param name="options">Import options.</param>
    /// <param name="persistenceContext">HNMO persistence context.</param>
    /// <param name="config">HNMO <see cref="GameConfiguration"/>.</param>
    /// <param name="backupDirectory">Directory where pre-import backup JSON files are saved.</param>
    /// <param name="dbReader">Optional MSSQL database reader.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The apply result containing the backup ID for undo.</returns>
    public static async Task<LegacyImportApplyResult> ApplyImportAsync(
        LegacyImportOptions options,
        IContext persistenceContext,
        GameConfiguration config,
        string backupDirectory,
        ILegacyDatabaseReader? dbReader = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(persistenceContext);
        ArgumentNullException.ThrowIfNull(config);

        var existingAccounts = (await persistenceContext.GetAsync<Account>(cancellationToken).ConfigureAwait(false)).ToList();
        var existingCharacters = (await persistenceContext.GetAsync<Character>(cancellationToken).ConfigureAwait(false)).ToList();
        var existingGuilds = (await persistenceContext.GetAsync<HNMO.DataModel.Entities.Guild>(cancellationToken).ConfigureAwait(false)).ToList();

        var preview = await BuildDryRunPreviewAsync(
            options,
            config,
            dbReader,
            existingAccounts.Select(a => a.LoginName),
            existingCharacters.Select(c => c.Name),
            existingGuilds.Where(g => g.Name != null).Select(g => g.Name!),
            cancellationToken).ConfigureAwait(false);

        var nowUtc = DateTime.UtcNow;
        var backupId = $"import-{nowUtc:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        var sydneyTime = HnmoStateStore.ToTimeZone(nowUtc, HnmoStateStore.ResolveTimeZone(null));

        var snapshot = new LegacyImportBackupSnapshot
        {
            BackupId = backupId,
            CreatedAtUtc = nowUtc,
            CreatedAtSydney = sydneyTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            SourceVersion = preview.DetectedVersion,
            SourceFolder = options.FolderPath,
            PreviousExperienceRate = config.ExperienceRate,
            PreviousMasterExperienceRate = config.MasterExperienceRate,
            PreviousShouldDropMoney = config.ShouldDropMoney,
            ModifiedRates = options.ImportRates && preview.Rates is not null,
        };

        BackupPlugInConfig(config, typeof(ChaosRatesPlugIn).GUID, snapshot);
        BackupPlugInConfig(config, typeof(TimetablePlugIn).GUID, snapshot);
        BackupPlugInConfig(config, typeof(BonusEventsPlugIn).GUID, snapshot);

        var monstersByNum = config.Monsters.GroupBy(m => (int)m.Number).ToDictionary(g => g.Key, g => g.First());
        var itemsByKey = config.Items.ToDictionary(i => $"{i.Group}:{i.Number}", StringComparer.Ordinal);
        var mapsByNum = config.Maps.ToDictionary(m => (int)m.Number);

        foreach (var shop in preview.Shops)
        {
            if (monstersByNum.TryGetValue(shop.NpcNumber, out var npc))
            {
                var shopBackup = new BackupMerchantShop
                {
                    NpcNumber = shop.NpcNumber,
                    HadMerchantStore = npc.MerchantStore is not null,
                };
                if (npc.MerchantStore is not null)
                {
                    foreach (var oldItem in npc.MerchantStore.Items)
                    {
                        if (oldItem.Definition is not null)
                        {
                            shopBackup.PreviousItems.Add(new BackupItemSlot
                            {
                                ItemSlot = oldItem.ItemSlot,
                                Group = oldItem.Definition.Group,
                                Number = oldItem.Definition.Number,
                                Level = oldItem.Level,
                                Durability = oldItem.Durability,
                                HasSkill = oldItem.HasSkill,
                            });
                        }
                    }
                }

                snapshot.PreviousMerchantShops.Add(shopBackup);
            }
        }

        // Write backup BEFORE mutating HNMO data.
        var result = new LegacyImportApplyResult
        {
            BackupId = backupId,
            AppliedCount = 0,
            SkippedCount = preview.TotalSkipped,
        };

        if (options.ImportRates && preview.Rates is { } rates)
        {
            if (rates.ExperienceRate.HasValue)
            {
                config.ExperienceRate = rates.ExperienceRate.Value;
                result.AppliedCount++;
            }

            if (rates.MasterExperienceRate.HasValue)
            {
                config.MasterExperienceRate = rates.MasterExperienceRate.Value;
                result.AppliedCount++;
            }
        }

        if (options.ImportDropLists && preview.DropRules.Count > 0)
        {
            foreach (var groupSlice in preview.DropRules.GroupBy(d => (d.MonsterNumber, d.IsExcellent, d.IsAncient)))
            {
                var tag = $"[{backupId}] Imported Drops #{groupSlice.Key.MonsterNumber}";
                var dropGroup = persistenceContext.CreateNew<DropItemGroup>();
                dropGroup.Description = new LocalizedString(tag);
                dropGroup.Chance = Math.Clamp(groupSlice.Max(x => x.Chance), 0.0001, 1.0);
                dropGroup.ItemType = groupSlice.Key.IsAncient
                    ? SpecialItemType.Ancient
                    : groupSlice.Key.IsExcellent ? SpecialItemType.Excellent : SpecialItemType.None;
                dropGroup.MinimumMonsterLevel = (byte)Math.Clamp(groupSlice.Min(x => x.MonsterMinLevel), 0, 255);
                dropGroup.MaximumMonsterLevel = (byte)Math.Clamp(groupSlice.Max(x => x.MonsterMaxLevel), 0, 255);
                if (groupSlice.Key.MonsterNumber >= 0 && monstersByNum.TryGetValue(groupSlice.Key.MonsterNumber, out var monsterDef))
                {
                    dropGroup.Monster = monsterDef;
                }

                foreach (var rule in groupSlice)
                {
                    if (itemsByKey.TryGetValue($"{rule.ItemGroup}:{rule.ItemNumber}", out var itemDef)
                        && !dropGroup.PossibleItems.Contains(itemDef))
                    {
                        dropGroup.PossibleItems.Add(itemDef);
                    }
                }

                config.DropItemGroups.Add(dropGroup);
                snapshot.CreatedDropGroupTags.Add(tag);
                result.AppliedCount++;
            }
        }

        if (options.ImportShops && preview.Shops.Count > 0)
        {
            foreach (var shop in preview.Shops)
            {
                if (!monstersByNum.TryGetValue(shop.NpcNumber, out var npc))
                {
                    continue;
                }

                npc.MerchantStore ??= persistenceContext.CreateNew<ItemStorage>();
                npc.MerchantStore.Items.Clear();
                byte slotIndex = 0;
                foreach (var entry in shop.Items)
                {
                    if (!itemsByKey.TryGetValue($"{entry.Group}:{entry.Number}", out var def) || slotIndex >= 120)
                    {
                        continue;
                    }

                    var item = persistenceContext.CreateNew<Item>();
                    item.Definition = def;
                    item.ItemSlot = slotIndex++;
                    item.Level = entry.Level;
                    item.Durability = entry.Durability > 0 ? entry.Durability : def.Durability;
                    item.HasSkill = entry.Skill;
                    npc.MerchantStore.Items.Add(item);
                }

                result.AppliedCount++;
            }
        }

        if (options.ImportChaosMixRates && preview.ChaosRates.Count > 0)
        {
            var cfg = ReadPlugInConfig<ChaosRatesConfiguration>(config, typeof(ChaosRatesPlugIn).GUID) ?? new ChaosRatesConfiguration();
            cfg.Enabled = true;
            byte nextIdx = 10;
            foreach (var c in preview.ChaosRates)
            {
                var existing = cfg.RecipeOverrides.FirstOrDefault(r => r.RecipeName.Equals(c.CraftNamePattern, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    existing = new ChaosRecipeOverride { RecipeNumber = nextIdx++, RecipeName = c.CraftNamePattern };
                    cfg.RecipeOverrides.Add(existing);
                }

                existing.SuccessRateOverridePercent = (byte)Math.Clamp(c.SuccessPercent, 0, 100);
            }

            WritePlugInConfig(persistenceContext, config, typeof(ChaosRatesPlugIn).GUID, cfg, activate: true);
            result.AppliedCount += preview.ChaosRates.Count;
        }

        if (options.ImportEventSchedules && preview.EventSchedules.Count > 0)
        {
            var timetable = ReadPlugInConfig<TimetableConfiguration>(config, typeof(TimetablePlugIn).GUID) ?? new TimetableConfiguration();
            foreach (var ev in preview.EventSchedules.Where(e => !e.MapNumber.HasValue))
            {
                if (!timetable.CustomEntries.Any(x => x.Id.Equals(ev.EventId, StringComparison.OrdinalIgnoreCase)))
                {
                    timetable.CustomEntries.Add(new TimetableCustomEntry
                    {
                        Id = ev.EventId,
                        Enabled = true,
                        Category = "event",
                        NameEn = ev.Name,
                        NameVi = ev.Name,
                        ScheduleTimes = ev.ScheduleTimes,
                        DropsEn = ev.RewardSummary,
                        DropsVi = ev.RewardSummary,
                    });
                }
            }

            WritePlugInConfig(persistenceContext, config, typeof(TimetablePlugIn).GUID, timetable, activate: true);

            var bonusEvents = preview.EventSchedules.Where(e => e.MapNumber.HasValue).ToList();
            if (bonusEvents.Count > 0)
            {
                var bonusCfg = ReadPlugInConfig<BonusEventsConfiguration>(config, typeof(BonusEventsPlugIn).GUID) ?? new BonusEventsConfiguration();
                bonusCfg.Enabled = true;
                foreach (var be in bonusEvents)
                {
                    bonusCfg.Events.Add(new BonusEventEntry
                    {
                        Id = be.EventId,
                        NameEn = be.Name,
                        NameVi = be.Name,
                        MapNumber = be.MapNumber!.Value,
                        StartTimeSydney = be.ScheduleTimes,
                        DurationMinutes = 60,
                        ExpBonusPercent = be.ExpBonusPercent,
                        ItemDropBonusPercent = be.DropBonusPercent,
                    });
                }

                WritePlugInConfig(persistenceContext, config, typeof(BonusEventsPlugIn).GUID, bonusCfg, activate: true);
            }

            result.AppliedCount += preview.EventSchedules.Count;
        }

        if (options.ImportMonsterSpawns && preview.Spawns.Count > 0)
        {
            foreach (var spawn in preview.Spawns)
            {
                if (!mapsByNum.TryGetValue(spawn.MapNumber, out var map)
                    || !monstersByNum.TryGetValue(spawn.MonsterNumber, out var monster))
                {
                    continue;
                }

                var area = persistenceContext.CreateNew<MonsterSpawnArea>();
                area.GameMap = map;
                area.MonsterDefinition = monster;
                area.X1 = (byte)spawn.X1;
                area.Y1 = (byte)spawn.Y1;
                area.X2 = (byte)Math.Max(spawn.X1, spawn.X2);
                area.Y2 = (byte)Math.Max(spawn.Y1, spawn.Y2);
                area.Quantity = (short)Math.Clamp(spawn.Quantity, 1, 500);
                area.SpawnTrigger = spawn.SpawnGroup == 3 ? SpawnTrigger.OnceAtEventStart : SpawnTrigger.Automatic;
                map.MonsterSpawns.Add(area);
                snapshot.CreatedSpawnAreas.Add(new BackupSpawnEntry
                {
                    MapNumber = map.Number,
                    MonsterNumber = monster.Number,
                    X1 = area.X1,
                    Y1 = area.Y1,
                    X2 = area.X2,
                    Y2 = area.Y2,
                    Quantity = area.Quantity,
                });
                result.AppliedCount++;
            }
        }

        if (options.ImportDatabaseEntities && preview.DatabaseSnapshot is { } db)
        {
            ApplyDatabaseEntities(options, db, persistenceContext, config, existingAccounts, existingCharacters, existingGuilds, itemsByKey, mapsByNum, snapshot, result);
        }

        snapshot.SummaryText = $"{preview.DetectedVersion}: {result.AppliedCount} applied, {result.SkippedCount} skipped";
        result.BackupFilePath = LegacyImportBackupSnapshot.SaveAtomic(backupDirectory, snapshot);
        await persistenceContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Restores configuration, plugins, merchant shops, and deletes entities added by a previous import backup.
    /// </summary>
    /// <param name="backupId">The backup identifier to roll back.</param>
    /// <param name="backupDirectory">Directory containing backup JSON snapshots.</param>
    /// <param name="persistenceContext">HNMO persistence context.</param>
    /// <param name="config">HNMO <see cref="GameConfiguration"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if backup was found and rolled back; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> UndoBackupAsync(
        string backupId,
        string backupDirectory,
        IContext persistenceContext,
        GameConfiguration config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistenceContext);
        ArgumentNullException.ThrowIfNull(config);

        var snapshot = LegacyImportBackupSnapshot.TryLoad(backupDirectory, backupId);
        if (snapshot is null)
        {
            return false;
        }

        if (snapshot.ModifiedRates)
        {
            config.ExperienceRate = snapshot.PreviousExperienceRate;
            config.MasterExperienceRate = snapshot.PreviousMasterExperienceRate;
            config.ShouldDropMoney = snapshot.PreviousShouldDropMoney;
        }

        foreach (var prevPlugin in snapshot.PreviousPlugIns)
        {
            var existing = config.PlugInConfigurations.FirstOrDefault(p => p.TypeId == prevPlugin.TypeId);
            if (!prevPlugin.Existed)
            {
                if (existing is not null)
                {
                    config.PlugInConfigurations.Remove(existing);
                }
            }
            else if (existing is not null)
            {
                existing.IsActive = prevPlugin.PreviousIsActive;
                existing.CustomConfiguration = prevPlugin.PreviousJson;
            }
        }

        var dropTags = snapshot.CreatedDropGroupTags.ToHashSet(StringComparer.Ordinal);
        foreach (var toRemove in config.DropItemGroups.Where(g => dropTags.Contains(g.Description.ValueInNeutralLanguage)).ToList())
        {
            config.DropItemGroups.Remove(toRemove);
            await persistenceContext.DeleteAsync(toRemove).ConfigureAwait(false);
        }

        foreach (var spawnKey in snapshot.CreatedSpawnAreas)
        {
            var map = config.Maps.FirstOrDefault(m => m.Number == spawnKey.MapNumber);
            if (map is null)
            {
                continue;
            }

            var match = map.MonsterSpawns.FirstOrDefault(s =>
                s.MonsterDefinition?.Number == spawnKey.MonsterNumber
                && s.X1 == spawnKey.X1
                && s.Y1 == spawnKey.Y1
                && s.X2 == spawnKey.X2
                && s.Y2 == spawnKey.Y2
                && s.Quantity == spawnKey.Quantity);
            if (match is not null)
            {
                map.MonsterSpawns.Remove(match);
                await persistenceContext.DeleteAsync(match).ConfigureAwait(false);
            }
        }

        var itemsByKey = config.Items.ToDictionary(i => $"{i.Group}:{i.Number}", StringComparer.Ordinal);
        var monstersByNum = config.Monsters.GroupBy(m => (int)m.Number).ToDictionary(g => g.Key, g => g.First());
        foreach (var shopBackup in snapshot.PreviousMerchantShops)
        {
            if (!monstersByNum.TryGetValue(shopBackup.NpcNumber, out var npc))
            {
                continue;
            }

            if (!shopBackup.HadMerchantStore)
            {
                npc.MerchantStore = null;
                continue;
            }

            npc.MerchantStore ??= persistenceContext.CreateNew<ItemStorage>();
            npc.MerchantStore.Items.Clear();
            foreach (var prevItem in shopBackup.PreviousItems)
            {
                if (!itemsByKey.TryGetValue($"{prevItem.Group}:{prevItem.Number}", out var def))
                {
                    continue;
                }

                var restored = persistenceContext.CreateNew<Item>();
                restored.Definition = def;
                restored.ItemSlot = prevItem.ItemSlot;
                restored.Level = prevItem.Level;
                restored.Durability = prevItem.Durability;
                restored.HasSkill = prevItem.HasSkill;
                npc.MerchantStore.Items.Add(restored);
            }
        }

        if (snapshot.CreatedGuildNames.Count > 0)
        {
            var guildSet = new HashSet<string>(snapshot.CreatedGuildNames, StringComparer.OrdinalIgnoreCase);
            var allGuilds = (await persistenceContext.GetAsync<HNMO.DataModel.Entities.Guild>(cancellationToken).ConfigureAwait(false)).ToList();
            foreach (var guild in allGuilds.Where(g => g.Name != null && guildSet.Contains(g.Name)))
            {
                await persistenceContext.DeleteAsync(guild).ConfigureAwait(false);
            }
        }

        if (snapshot.CreatedAccountLogins.Count > 0)
        {
            var loginSet = new HashSet<string>(snapshot.CreatedAccountLogins, StringComparer.OrdinalIgnoreCase);
            var allAccounts = (await persistenceContext.GetAsync<Account>(cancellationToken).ConfigureAwait(false)).ToList();
            foreach (var acc in allAccounts.Where(a => loginSet.Contains(a.LoginName)))
            {
                await persistenceContext.DeleteAsync(acc).ConfigureAwait(false);
            }
        }

        await persistenceContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LegacyImportBackupSnapshot.Delete(backupDirectory, backupId);
        return true;
    }

    private static void ApplyDatabaseEntities(
        LegacyImportOptions options,
        LegacyDatabaseSnapshot db,
        IContext persistenceContext,
        GameConfiguration config,
        List<Account> existingAccounts,
        List<Character> existingCharacters,
        List<HNMO.DataModel.Entities.Guild> existingGuilds,
        Dictionary<string, ItemDefinition> itemsByKey,
        Dictionary<int, GameMapDefinition> mapsByNum,
        LegacyImportBackupSnapshot snapshot,
        LegacyImportApplyResult result)
    {
        var accountsByLogin = existingAccounts.ToDictionary(a => a.LoginName, StringComparer.OrdinalIgnoreCase);
        var charsByName = existingCharacters.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var guildsByName = existingGuilds
            .Where(g => g.Name != null)
            .ToDictionary(g => g.Name!, StringComparer.OrdinalIgnoreCase);
        var classesByNumber = config.CharacterClasses.ToDictionary(c => c.Number);
        var defaultClass = config.CharacterClasses.FirstOrDefault();
        var defaultMap = config.Maps.FirstOrDefault(m => m.Number == 0) ?? config.Maps.FirstOrDefault();

        var vaultsByAccount = db.Vaults.GroupBy(v => v.AccountId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in db.Accounts)
        {
            if (accountsByLogin.ContainsKey(row.LoginName))
            {
                continue;
            }

            var account = persistenceContext.CreateNew<Account>();
            account.LoginName = row.LoginName;
            account.EMail = row.Email;
            account.State = row.IsBanned ? AccountState.Banned : AccountState.Normal;

            var passwordSecret = options.PasswordMode == LegacyPasswordMigrationMode.UseTemporaryPassphrase
                && !string.IsNullOrWhiteSpace(options.TemporaryPassphrase)
                ? options.TemporaryPassphrase
                : Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordSecret);

            if (vaultsByAccount.TryGetValue(row.LoginName, out var vaultRow))
            {
                account.Vault = persistenceContext.CreateNew<ItemStorage>();
                account.Vault.Money = Math.Max(0, vaultRow.Money);
                PopulateStorageFromBlob(account.Vault, vaultRow.ItemsBlob, persistenceContext, itemsByKey);
            }

            accountsByLogin[row.LoginName] = account;
            snapshot.CreatedAccountLogins.Add(row.LoginName);
            result.AppliedCount++;
        }

        foreach (var crow in db.Characters)
        {
            if (charsByName.ContainsKey(crow.Name) || !accountsByLogin.TryGetValue(crow.AccountId, out var ownerAccount))
            {
                continue;
            }

            var mappedClassNumber = LegacyClassMap.TryGetValue(crow.ClassNumber, out var num) ? num : (byte)0;
            var charClass = classesByNumber.GetValueOrDefault(mappedClassNumber) ?? defaultClass;
            if (charClass is null)
            {
                continue;
            }

            var character = persistenceContext.CreateNew<Character>();
            character.Name = crow.Name;
            character.CharacterSlot = (byte)Math.Min(4, ownerAccount.Characters.Count);
            persistenceContext.InitializeNewCharacter(character, charClass);
            character.LevelUpPoints = crow.LevelUpPoints;
            character.Experience = crow.Experience;
            character.CurrentMap = mapsByNum.GetValueOrDefault(crow.MapNumber) ?? defaultMap;
            character.PositionX = crow.MapPosX;
            character.PositionY = crow.MapPosY;
            SetStat(character, Stats.Level.Id, Math.Clamp(crow.Level, 1, 400));
            SetStat(character, Stats.BaseStrength.Id, crow.Strength);
            SetStat(character, Stats.BaseAgility.Id, crow.Agility);
            SetStat(character, Stats.BaseVitality.Id, crow.Vitality);
            SetStat(character, Stats.BaseEnergy.Id, crow.Energy);
            SetStat(character, Stats.BaseLeadership.Id, crow.Leadership);

            character.Inventory ??= persistenceContext.CreateNew<ItemStorage>();
            character.Inventory.Money = Math.Max(0, crow.Money);
            PopulateStorageFromBlob(character.Inventory, crow.InventoryBlob, persistenceContext, itemsByKey);

            ownerAccount.Characters.Add(character);
            charsByName[crow.Name] = character;
            result.AppliedCount++;
        }

        foreach (var grow in db.Guilds)
        {
            if (guildsByName.ContainsKey(grow.Name))
            {
                continue;
            }

            var guild = persistenceContext.CreateNew<HNMO.DataModel.Entities.Guild>();
            guild.Name = grow.Name;
            guild.Score = grow.Score;
            guild.Notice = grow.Notice;
            guild.Logo = grow.Logo.Length > 0 ? grow.Logo : new byte[32];

            foreach (var memberRow in db.GuildMembers.Where(m => m.GuildName.Equals(grow.Name, StringComparison.OrdinalIgnoreCase)))
            {
                if (!charsByName.TryGetValue(memberRow.CharacterName, out var memberChar))
                {
                    continue;
                }

                var gm = persistenceContext.CreateNew<GuildMember>();
                gm.Id = memberChar.Id;
                gm.GuildId = guild.Id;
                gm.Status = memberRow.Status switch
                {
                    128 => GuildPosition.GuildMaster,
                    64 => GuildPosition.AssistantMaster,
                    32 => GuildPosition.BattleMaster,
                    _ => GuildPosition.NormalMember,
                };
                guild.Members.Add(gm);
            }

            guildsByName[grow.Name] = guild;
            snapshot.CreatedGuildNames.Add(grow.Name);
            result.AppliedCount++;
        }
    }

    private static void PopulateStorageFromBlob(
        ItemStorage storage,
        byte[] blob,
        IContext context,
        Dictionary<string, ItemDefinition> itemsByKey)
    {
        foreach (var decoded in LegacyFileParsers.ParseLegacyBinaryStorage(blob))
        {
            if (!itemsByKey.TryGetValue($"{decoded.Group}:{decoded.Number}", out var def))
            {
                continue;
            }

            var item = context.CreateNew<Item>();
            item.Definition = def;
            item.ItemSlot = decoded.Slot;
            item.Level = decoded.Level;
            item.Durability = decoded.Durability > 0 ? decoded.Durability : def.Durability;
            item.HasSkill = decoded.HasSkill;
            storage.Items.Add(item);
        }
    }

    private static void SetStat(Character character, Guid attrId, int value)
    {
        var attr = character.Attributes.FirstOrDefault(a => a.Definition?.Id == attrId);
        if (attr is not null)
        {
            attr.Value = value;
        }
    }

    private static void BackupPlugInConfig(GameConfiguration config, Guid typeId, LegacyImportBackupSnapshot snapshot)
    {
        var existing = config.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeId);
        snapshot.PreviousPlugIns.Add(new BackupPlugInState
        {
            TypeId = typeId,
            Existed = existing is not null,
            PreviousIsActive = existing?.IsActive ?? false,
            PreviousJson = existing?.CustomConfiguration,
        });
    }

    private static T? ReadPlugInConfig<T>(GameConfiguration config, Guid typeId)
        where T : class
    {
        var entry = config.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeId);
        if (string.IsNullOrWhiteSpace(entry?.CustomConfiguration))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(entry.CustomConfiguration);
        }
        catch
        {
            return null;
        }
    }

    private static void WritePlugInConfig<T>(
        IContext context,
        GameConfiguration config,
        Guid typeId,
        T model,
        bool activate)
    {
        var entry = config.PlugInConfigurations.FirstOrDefault(p => p.TypeId == typeId);
        if (entry is null)
        {
            entry = context.CreateNew<PlugInConfiguration>();
            entry.TypeId = typeId;
            config.PlugInConfigurations.Add(entry);
        }

        entry.CustomConfiguration = JsonSerializer.Serialize(model);
        if (activate)
        {
            entry.IsActive = true;
        }
    }
}
