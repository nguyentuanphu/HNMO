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
using HNMO.GameLogic.PlugIns.ChatCommands;
using HNMO.GameLogic.PlugIns.GameplayRules;
using HNMO.GameLogic.PlugIns.PeriodicTasks;
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
    /// <summary>The craftings of the item upgrades +10 to +15 (VersionSeasonSix/ChaosMixes.cs, ItemLevelUpgradeCrafting).</summary>
    private static readonly Dictionary<string, byte> UpgradeCraftingNumbers = new(StringComparer.Ordinal)
    {
        ["+10"] = 3, ["+11"] = 4, ["+12"] = 22, ["+13"] = 23, ["+14"] = 49, ["+15"] = 50,
    };

    private const int MaximumSupportedMasterLevel = 200;

    private const int ShopColumns = 8;

    private const int ShopRows = 15;

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
        // Several maps can share a number (Devil Square 1-4 are all map 9): the first one with it stands for it, as for monsters.
        var mapsByNumber = config.Maps.GroupBy(m => (int)m.Number).ToDictionary(g => g.Key, g => g.First());
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
                details.Add($"Drop %={rates.ItemDropPercent?.ToString(CultureInfo.InvariantCulture) ?? "-"}, excellent {rates.ExcellentItemDropPerTenThousand?.ToString(CultureInfo.InvariantCulture) ?? "-"}/10000");
                mapped++;
            }

            if (rates.ZenMultiplier is { } zenTerm)
            {
                details.Add($"ZenDropMultipler={zenTerm.ToString(CultureInfo.InvariantCulture)}: the master-level zen bonus, not a zen multiplier; not applied");
            }

            if (rates.SoulSuccessPercent.HasValue || rates.LifeSuccessPercent.HasValue)
            {
                details.Add($"Jewel of Soul {rates.SoulSuccessPercent?.ToString(CultureInfo.InvariantCulture) ?? "-"}% ({rates.SoulWithLuckSuccessPercent?.ToString(CultureInfo.InvariantCulture) ?? "-"}% with luck), Jewel of Life {rates.LifeSuccessPercent?.ToString(CultureInfo.InvariantCulture) ?? "-"}%");
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
            var rewardBags = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in rawDrops)
            {
                // A box's content: what a player gets when opening the box.
                if (d.BoxItem is { } box && itemsByKey.ContainsKey($"{box.Group}:{box.Number}") && itemsByKey.ContainsKey($"{d.ItemGroup}:{d.ItemNumber}")
                    && d.ElementalAttribute == 0 && !d.CompanionEvolutionGroup.HasValue)
                {
                    preview.BoxContents.Add(d);
                    continue;
                }

                // Blood Castle and Chaos Castle rewards: what the winners get.
                if (d.EventReward is not null && itemsByKey.ContainsKey($"{d.ItemGroup}:{d.ItemNumber}")
                    && d.ElementalAttribute == 0 && !d.CompanionEvolutionGroup.HasValue)
                {
                    preview.EventRewards.Add(d);
                    continue;
                }

                if (d.RewardBagName is { } bagName)
                {
                    // Boxes, event and chaos-mix rewards, NPC gifts: not a monster's drop, and not mapped yet.
                    skippedDrops++;
                    rewardBags[bagName] = rewardBags.GetValueOrDefault(bagName) + 1;
                    continue;
                }

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

            if (rewardBags.Count > 0)
            {
                preview.UnmappedWarnings.Add($"{rewardBags.Count} item bags are box, event, chaos-mix or NPC rewards, not monster drops, and are not imported: {string.Join(", ", rewardBags.Select(b => $"{b.Key} ({b.Value})"))}.");
            }

            preview.Categories.Add(new LegacyPreviewCategory
            {
                NameEn = "Item Drop Lists",
                NameVi = "Danh Sách Rơi Vật Phẩm",
                MappedCount = mappedDrops,
                SkippedCount = skippedDrops,
                Details = [$"{preview.DropRules.Select(d => d.MonsterNumber).Where(m => m >= 0).Distinct().Count()} monsters get their own drops; {preview.BoxContents.Select(b => b.BoxItem).Distinct().Count()} boxes get contents; {preview.EventRewards.Select(b => b.EventReward).Distinct().Count()} Blood/Chaos Castle rewards; {rewardBags.Count} other event, mix and NPC reward bags left out"],
            });
        }

        if (options.ImportShops)
        {
            var rawShops = LegacyFileParsers.ParseShops(options.FolderPath);
            var mappedShops = 0;
            var skippedShops = 0;
            var shopNpcs = new HashSet<int>();
            foreach (var shop in rawShops)
            {
                if (!monstersByNumber.TryGetValue(shop.NpcNumber, out _))
                {
                    skippedShops++;
                    preview.UnmappedWarnings.Add($"Shop NPC #{shop.NpcNumber} (map {shop.MapNumber}) skipped: NPC not defined in HNMO.");
                    continue;
                }

                // An empty shop file would empty the store HNMO has; a second shop of the same NPC would replace the
                // first one (an NPC has one store).
                if (shop.Items.Count == 0)
                {
                    skippedShops++;
                    preview.UnmappedWarnings.Add($"Shop NPC #{shop.NpcNumber} (map {shop.MapNumber}) skipped: its shop file has no items, HNMO's store is kept.");
                    continue;
                }

                if (!shopNpcs.Add(shop.NpcNumber))
                {
                    skippedShops++;
                    preview.UnmappedWarnings.Add($"Shop NPC #{shop.NpcNumber} (map {shop.MapNumber}) skipped: the NPC already has a shop from the list.");
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
            var eventSpawns = 0;
            foreach (var spawn in rawSpawns)
            {
                if (spawn.SpawnGroup >= 5)
                {
                    skippedSpawns++;
                    preview.UnmappedWarnings.Add($"Spawn group {spawn.SpawnGroup} for monster #{spawn.MonsterNumber} skipped: fortress/elemental script trigger must be configured natively.");
                    continue;
                }

                // Group 3 is the invasion monsters (golden monsters, Kundun's army) and group 4 the Blood Castle,
                // Devil Square and Chaos Castle monsters: the events place them. Imported as normal spawns, the event
                // monsters stood on their maps all day and the invasion ones never came.
                if (spawn.SpawnGroup is 3 or 4)
                {
                    skippedSpawns++;
                    eventSpawns++;
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
                Details = [$"{eventSpawns} event and invasion spawns left to their events; spawns HNMO already has are not added again"],
            });
        }

        if (options.ImportMonsterStats)
        {
            var allStats = LegacyFileParsers.ParseMonsterStats(options.FolderPath);
            var unknown = 0;
            foreach (var stats in allStats)
            {
                if (!monstersByNumber.TryGetValue(stats.Number, out var monster))
                {
                    unknown++;
                    continue;
                }

                if (MonsterStatsDiffer(monster, stats))
                {
                    preview.MonsterStats.Add(stats);
                }
            }

            if (allStats.Count > 0)
            {
                preview.Categories.Add(new LegacyPreviewCategory
                {
                    NameEn = "Monster Stats",
                    NameVi = "Chỉ Số Quái",
                    MappedCount = preview.MonsterStats.Count,
                    SkippedCount = unknown,
                    Details = [$"{preview.MonsterStats.Count} of {allStats.Count - unknown} monsters differ from HNMO's (life, damage, defense, rates, ranges, speeds, respawn); {unknown} are not in HNMO"],
                });
            }
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
        BackupPlugInConfig(config, typeof(RateTuningPlugIn).GUID, snapshot);
        snapshot.PreviousCommonItemDropChance = CommonDropGroup(config, SpecialItemType.RandomItem)?.Chance;
        snapshot.PreviousExcellentItemDropChance = CommonDropGroup(config, SpecialItemType.Excellent)?.Chance;
        BackupPlugInConfig(config, typeof(TimetablePlugIn).GUID, snapshot);
        BackupPlugInConfig(config, typeof(BonusEventsPlugIn).GUID, snapshot);
        BackupPlugInConfig(config, typeof(GuildLimitsPlugIn).GUID, snapshot);
        BackupPlugInConfig(config, typeof(PkClearChatCommandPlugIn).GUID, snapshot);
        BackupPlugInConfig(config, typeof(DevilSquareStartPlugIn).GUID, snapshot);
        BackupPlugInConfig(config, typeof(ChaosCastleStartPlugIn).GUID, snapshot);
        BackupPlugInConfig(config, typeof(BloodCastleStartPlugIn).GUID, snapshot);
        snapshot.PreviousMaximumLevel = config.MaximumLevel;
        snapshot.PreviousMaximumMasterLevel = config.MaximumMasterLevel;
        snapshot.PreviousLuckAddChance = LuckOptionDefinition(config)?.AddChance;

        var monstersByNum = config.Monsters.GroupBy(m => (int)m.Number).ToDictionary(g => g.Key, g => g.First());
        var itemsByKey = config.Items.ToDictionary(i => $"{i.Group}:{i.Number}", StringComparer.Ordinal);
        // Several maps can share a number (Devil Square 1-4 are all map 9): a spawn of that number goes to the first one.
        var mapsByNum = config.Maps.GroupBy(m => (int)m.Number).ToDictionary(g => g.Key, g => g.First());

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
                                HasLuck = oldItem.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.Luck),
                                OptionLevel = (byte)(oldItem.ItemOptions.FirstOrDefault(o => o.ItemOption?.OptionType == ItemOptionTypes.Option)?.Level ?? 0),
                                ExcellentBits = (byte)oldItem.ItemOptions
                                    .Where(o => o.ItemOption?.OptionType == ItemOptionTypes.Excellent && o.ItemOption.Number is >= 1 and <= 6)
                                    .Aggregate(0, (bits, o) => bits | (1 << (o.ItemOption!.Number - 1))),
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

            // ZenDropMultipler (Common.ini) is not a multiplier of all zen: it scales the extra zen of master-level
            // characters (Money += exp / BaseMasterExperience * ZenDropMultipler). It is shown in the preview only.
            // The jewel success rates go to the rate tuning, which has the same settings.
            if (rates.SoulSuccessPercent.HasValue || rates.LifeSuccessPercent.HasValue || rates.ZenTableEnabled || rates.PartyExpBonusPercent.Count > 0)
            {
                var tuning = ReadPlugInConfig<RateTuningConfiguration>(config, typeof(RateTuningPlugIn).GUID) ?? new RateTuningConfiguration();
                tuning.Enabled = true;

                // ZenDrop.xml: the zen of a kill is an amount of the map's range (times the monster level, at a
                // chance), not the gained EXP; HNMO's zen follows the EXP, so with EXP x1000 the zen was x1000 too.
                if (rates.ZenTableEnabled)
                {
                    tuning.ZenTableEnabled = true;
                    tuning.ZenTableMultiplyByMonsterLevel = rates.ZenTableMultiplyByMonsterLevel;
                    tuning.ZenTableMultiplyChancePerTenThousand = rates.ZenTableMultiplyChancePerTenThousand;
                    tuning.ZenTable = rates.ZenTable
                        .Select(z => new ZenTableEntry { MapNumber = z.MapNumber, MinimumZen = z.Minimum, MaximumZen = z.Maximum })
                        .ToList();
                }

                // NormalParty2..5ExpBonus and SetParty3..5ExpBonus: the party's EXP of a kill by its size.
                if (rates.PartyExpBonusPercent.Count > 0)
                {
                    tuning.PartyExpBonuses = rates.PartyExpBonusPercent.OrderBy(p => p.Key)
                        .Select(p => new PartyExpBonusEntry
                        {
                            PartySize = p.Key,
                            ExpPercent = p.Value,
                            DifferentClassesExpPercent = rates.SetPartyExpBonusPercent.GetValueOrDefault(p.Key),
                        })
                        .ToList();
                }

                if (rates.SoulSuccessPercent is { } soul)
                {
                    tuning.OverrideSoulRates = true;
                    tuning.SoulSuccessRatePercent = soul;
                    tuning.SoulLuckBonusPercent = Math.Max(0, (rates.SoulWithLuckSuccessPercent ?? soul) - soul);
                }

                if (rates.LifeSuccessPercent is { } life)
                {
                    tuning.OverrideLifeRate = true;
                    tuning.LifeSuccessRatePercent = life;
                }

                WritePlugInConfig(persistenceContext, config, typeof(RateTuningPlugIn).GUID, tuning, activate: true);
                result.AppliedCount++;
            }

            // The servers' ItemDropPer (the chance that a killed monster drops an item, 1-100 %) and ExcItemDropPer
            // (excellent items, n/10000) are the chances of HNMO's two common drop groups: the random items (30 % by
            // default) and the excellent items (0.01 %). They were read for the preview, but never applied.
            if (rates.ItemDropPercent is { } dropPercent && CommonDropGroup(config, SpecialItemType.RandomItem) is { } itemGroup)
            {
                itemGroup.Chance = dropPercent / 100.0;
                result.AppliedCount++;
            }

            if (rates.ExcellentItemDropPerTenThousand is { } excellent && CommonDropGroup(config, SpecialItemType.Excellent) is { } excellentGroup)
            {
                excellentGroup.Chance = excellent / 10000.0;
                result.AppliedCount++;
            }

            // ItemLuckDrop: the chance that a dropped item has luck (HNMO: 25 %).
            if (rates.ItemLuckDropPercent is { } luckPercent && LuckOptionDefinition(config) is { } luckDefinition)
            {
                luckDefinition.AddChance = luckPercent / 100f;
                result.AppliedCount++;
            }

            if (rates.MaximumLevel is int maximumLevel and > 0 and <= short.MaxValue)
            {
                config.MaximumLevel = (short)maximumLevel;
                result.AppliedCount++;
            }

            // HNMO's master skill tree goes to master level 200 (Season 6); a higher one (Season 9: 330) is kept out.
            if (rates.MaximumMasterLevel is { } maximumMasterLevel)
            {
                if (maximumMasterLevel is > 0 and <= MaximumSupportedMasterLevel)
                {
                    config.MaximumMasterLevel = (short)maximumMasterLevel;
                    result.AppliedCount++;
                }
                else
                {
                    result.SummaryLines.Add($"MaxMasterLevel {maximumMasterLevel} not applied: HNMO's master levels go to {MaximumSupportedMasterLevel}.");
                }
            }

            // MapAttribute.xml ExpBonus is added to the server's EXP rate on the map (rate 1000 + 2 = x1.002).
            if (rates.ExperienceRate is float baseRate and > 0)
            {
                foreach (var (mapNumber, bonus) in rates.MapExpBonuses)
                {
                    foreach (var map in config.Maps.Where(m => m.Number == mapNumber))
                    {
                        snapshot.PreviousMapExpMultipliers.TryAdd(map.Number, map.ExpMultiplier);
                        map.ExpMultiplier = (baseRate + bonus) / baseRate;
                    }
                }
            }

            // PkClearCost: the zen of a PK clear per kill. The plug-in keeps its on/off state (it is on when it has
            // no settings yet).
            if (rates.PkClearCost is int pkClearCost and > 0)
            {
                var pkClearId = typeof(PkClearChatCommandPlugIn).GUID;
                var pkClear = ReadPlugInConfig<PkClearChatCommandPlugIn.PKClearConfiguration>(config, pkClearId) ?? new PkClearChatCommandPlugIn.PKClearConfiguration();
                pkClear.ZenCostPerKill = pkClearCost;
                WritePlugInConfig(persistenceContext, config, pkClearId, pkClear, activate: config.PlugInConfigurations.All(p => p.TypeId != pkClearId));
                result.AppliedCount++;
            }

            if (rates.GuildCreateLevel is > 0 || rates.MaximumGuildMembers is > 0)
            {
                var guildLimits = new GuildLimitsConfiguration
                {
                    MinimumCreateLevel = Math.Max(0, rates.GuildCreateLevel ?? 0),
                    MaximumMembers = Math.Max(0, rates.MaximumGuildMembers ?? 0),
                };
                WritePlugInConfig(persistenceContext, config, typeof(GuildLimitsPlugIn).GUID, guildLimits, activate: true);
                result.AppliedCount++;
            }
        }

        if (options.ImportDropLists && preview.DropRules.Count > 0)
        {
            // A group is what one monster (or one map, or every map) drops of one kind: normal, excellent or ancient.
            // Its chance is the source's own group chance (an item bag's ItemRate) or the sum of its items' chances;
            // it was the highest single item's chance, so a group of many rare items dropped as often as its best one.
            // It is attached to its monster or map: a group only in GameConfiguration.DropItemGroups is never rolled
            // (DefaultDropGenerator reads the monster's, the map's and the character's groups), so the imported drops
            // did nothing in game.
            var bagTotals = preview.DropRules.Where(d => d.GroupChance.HasValue)
                .GroupBy(d => (d.MonsterNumber, d.MapNumber, d.SourceName))
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Chance));
            foreach (var groupSlice in preview.DropRules.GroupBy(d => (d.MonsterNumber, d.MapNumber, d.IsExcellent, d.IsAncient, d.SourceName)))
            {
                var (monsterNumber, mapNumber, isExcellent, isAncient, sourceName) = groupSlice.Key;
                var targetMonster = monsterNumber >= 0 ? monstersByNum.GetValueOrDefault(monsterNumber) : null;
                var targetMaps = monsterNumber >= 0 ? new List<GameMapDefinition>()
                    : mapNumber >= 0 ? config.Maps.Where(m => m.Number == mapNumber).ToList()
                    : config.Maps.ToList();
                if (targetMonster is null && targetMaps.Count == 0)
                {
                    result.SkippedCount++;
                    continue;
                }

                var tag = $"[{backupId}] Imported Drops #{monsterNumber}" + (mapNumber >= 0 ? $" map {mapNumber}" : string.Empty)
                    + (sourceName is null ? string.Empty : $" ({sourceName})") + (isExcellent ? " exc" : string.Empty) + (isAncient ? " anc" : string.Empty);
                var dropGroup = persistenceContext.CreateNew<DropItemGroup>();
                dropGroup.Description = new LocalizedString(tag);
                // A bag is split into its normal and its excellent group: each gets its share of the bag's chance, as
                // each item of the bag has its share; each got the whole chance, so the bag dropped twice as often.
                var groupChance = groupSlice.Select(x => x.GroupChance).FirstOrDefault(c => c.HasValue) is { } bagChance
                    ? bagChance * groupSlice.Sum(x => x.Chance) / Math.Max(1e-12, bagTotals.GetValueOrDefault((monsterNumber, mapNumber, sourceName)))
                    : groupSlice.Sum(x => x.Chance);
                dropGroup.Chance = Math.Clamp(groupChance, 0.000001, 1.0);
                dropGroup.ItemType = isAncient
                    ? SpecialItemType.Ancient
                    : isExcellent ? SpecialItemType.Excellent : SpecialItemType.None;
                dropGroup.MinimumMonsterLevel = (byte)Math.Clamp(groupSlice.Min(x => x.MonsterMinLevel), 0, 255);
                dropGroup.MaximumMonsterLevel = (byte)Math.Clamp(groupSlice.Max(x => x.MonsterMaxLevel), 0, 255);
                if (groupSlice.Select(x => x.MinItemLevel).Distinct().ToList() is [var itemLevel] && itemLevel > 0)
                {
                    dropGroup.ItemLevel = itemLevel;
                }

                dropGroup.Monster = targetMonster;
                foreach (var rule in groupSlice)
                {
                    if (itemsByKey.TryGetValue($"{rule.ItemGroup}:{rule.ItemNumber}", out var itemDef)
                        && !dropGroup.PossibleItems.Contains(itemDef))
                    {
                        dropGroup.PossibleItems.Add(itemDef);
                    }
                }

                config.DropItemGroups.Add(dropGroup);
                targetMonster?.DropItemGroups.Add(dropGroup);
                foreach (var map in targetMaps)
                {
                    map.DropItemGroups.Add(dropGroup);
                }

                snapshot.CreatedDropGroupTags.Add(tag);
                result.AppliedCount++;
            }
        }

        // Boxes (Item_(group,number,level) bags): what a player gets on opening the box. A box HNMO already fills at
        // that level keeps HNMO's content; the others get the server's.
        if (options.ImportDropLists && preview.BoxContents.Count > 0)
        {
            var kept = 0;
            foreach (var box in preview.BoxContents.GroupBy(b => b.BoxItem!.Value))
            {
                var boxDefinition = itemsByKey[$"{box.Key.Group}:{box.Key.Number}"];
                if (boxDefinition.DropItems.Any(g => g.SourceItemLevel == box.Key.Level))
                {
                    kept++;
                    continue;
                }

                var tag = $"[{backupId}] Imported Box {box.Key.Group}:{box.Key.Number}+{box.Key.Level}";
                var content = persistenceContext.CreateNew<ItemDropItemGroup>();
                content.Description = new LocalizedString(tag);
                content.SourceItemLevel = box.Key.Level;
                content.Chance = 1.0;
                content.ItemType = SpecialItemType.None;
                foreach (var rule in box)
                {
                    var item = itemsByKey[$"{rule.ItemGroup}:{rule.ItemNumber}"];
                    if (!content.PossibleItems.Contains(item))
                    {
                        content.PossibleItems.Add(item);
                    }
                }

                boxDefinition.DropItems.Add(content);
                snapshot.CreatedBoxContentTags.Add(tag);
                result.AppliedCount++;
            }

            if (kept > 0)
            {
                result.SummaryLines.Add($"{kept} boxes keep HNMO's own contents.");
            }
        }

        // Event_BloodCastle(N)_Reward / Event_ChaosCastle(N)_Reward: the items the winners of level N get. They take the
        // place of the items of HNMO's item reward of that level (Blood Castle's reward, Chaos Castle's jewels).
        if (options.ImportDropLists && preview.EventRewards.Count > 0)
        {
            foreach (var bag in preview.EventRewards.GroupBy(r => r.EventReward!.Value))
            {
                var type = bag.Key.Event.Equals("BloodCastle", StringComparison.OrdinalIgnoreCase) ? MiniGameType.BloodCastle : MiniGameType.ChaosCastle;
                var reward = config.MiniGameDefinitions
                    .Where(g => g.Type == type && g.GameLevel == bag.Key.Level)
                    .SelectMany(g => g.Rewards)
                    .FirstOrDefault(r => r.RewardType == MiniGameRewardType.ItemDrop && r.ItemReward is { ItemType: SpecialItemType.None or SpecialItemType.RandomItem, PossibleItems.Count: > 0 });
                if (reward?.ItemReward is not { } group)
                {
                    result.SummaryLines.Add($"{bag.Key.Event} {bag.Key.Level} reward skipped: HNMO has no item reward for it.");
                    continue;
                }

                snapshot.PreviousEventRewards.Add(new BackupEventReward
                {
                    MiniGameType = (int)type,
                    Level = bag.Key.Level,
                    Chance = group.Chance,
                    ItemKeys = group.PossibleItems.Select(i => $"{i.Group}:{i.Number}").ToList(),
                });
                group.PossibleItems.Clear();
                foreach (var rule in bag)
                {
                    var item = itemsByKey[$"{rule.ItemGroup}:{rule.ItemNumber}"];
                    if (!group.PossibleItems.Contains(item))
                    {
                        group.PossibleItems.Add(item);
                    }
                }

                group.Chance = Math.Clamp(bag.First().GroupChance ?? 1.0, 0.000001, 1.0);
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

                // A store is a grid 8 cells wide and 15 high, and an item's slot is the cell of its top left corner
                // (row * 8 + column). The items were given slots 0, 1, 2 ..., so a sword (1x3) next to a shield
                // (2x2) overlapped it and the shop window broke. Each item now takes the first free place it fits in.
                var occupied = new bool[ShopRows, ShopColumns];
                var dropped = 0;
                foreach (var entry in shop.Items)
                {
                    if (!itemsByKey.TryGetValue($"{entry.Group}:{entry.Number}", out var def))
                    {
                        continue;
                    }

                    if (FindShopSlot(occupied, Math.Max((byte)1, def.Width), Math.Max((byte)1, def.Height)) is not { } slot)
                    {
                        dropped++;
                        continue;
                    }

                    var item = persistenceContext.CreateNew<Item>();
                    item.Definition = def;
                    item.ItemSlot = slot;
                    item.Level = entry.Level;
                    item.Durability = entry.Durability > 0 ? entry.Durability : def.Durability;
                    item.HasSkill = entry.Skill;
                    AddLegacyOptions(persistenceContext, item, entry.Luck, entry.OptionLevel, entry.ExcellentBits);
                    npc.MerchantStore.Items.Add(item);
                }

                if (dropped > 0)
                {
                    result.SummaryLines.Add($"Shop NPC #{shop.NpcNumber}: {dropped} items did not fit in the store window and were left out.");
                }

                result.AppliedCount++;
            }
        }

        if (options.ImportChaosMixRates && preview.ChaosRates.Count > 0)
        {
            var craftings = config.Monsters.SelectMany(m => m.ItemCraftings)
                .Where(c => c.SimpleCraftingSettings is not null)
                .GroupBy(c => c.Number)
                .ToDictionary(g => g.Key, g => g.First());
            var applied = 0;
            foreach (var c in preview.ChaosRates)
            {
                if (c.MaximumOfCrafting is { } maximumOf)
                {
                    if (!craftings.TryGetValue(maximumOf, out var capped))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    var cappedSettings = capped.SimpleCraftingSettings!;
                    if (snapshot.PreviousCraftingRates.All(b => b.CraftingNumber != maximumOf))
                    {
                        snapshot.PreviousCraftingRates.Add(new BackupCraftingRate
                        {
                            CraftingNumber = maximumOf,
                            SuccessPercent = cappedSettings.SuccessPercent,
                            LuckBonusPercent = cappedSettings.SuccessPercentageAdditionForLuck,
                            MaximumSuccessPercent = cappedSettings.MaximumSuccessPercent,
                        });
                    }

                    cappedSettings.MaximumSuccessPercent = (byte)Math.Clamp(c.SuccessPercent, 1, 100);
                    applied++;
                    continue;
                }

                // The rate goes to the crafting it is for: the +10..+15 item upgrades are craftings 3, 4, 22, 23, 49
                // and 50 (VersionSeasonSix/ChaosMixes.cs). They were numbered 10-15 here, which are First Wings,
                // Dark Horse, Dark Raven and Potion of Bless, so the imported rates changed those instead.
                if (!UpgradeCraftingNumbers.TryGetValue(c.CraftNamePattern, out var recipeNumber)
                    || !craftings.TryGetValue(recipeNumber, out var crafting))
                {
                    result.SkippedCount++;
                    result.SummaryLines.Add($"Chaos rate {c.Key} skipped: no HNMO crafting for '{c.CraftNamePattern}'.");
                    continue;
                }

                // Mix1xRate_Normal is the base rate of the upgrade and MixAddLuckRate_Normal what luck adds to it, as
                // HNMO's crafting settings have them (60 % and +25 % for +10). The rate was set as a fixed override of
                // the chaos rates plug-in, which replaced the rate with luck, so an item with luck lost its 25 %.
                var settings = crafting.SimpleCraftingSettings!;
                if (snapshot.PreviousCraftingRates.All(b => b.CraftingNumber != recipeNumber))
                {
                    snapshot.PreviousCraftingRates.Add(new BackupCraftingRate
                    {
                        CraftingNumber = recipeNumber,
                        SuccessPercent = settings.SuccessPercent,
                        LuckBonusPercent = settings.SuccessPercentageAdditionForLuck,
                        MaximumSuccessPercent = settings.MaximumSuccessPercent,
                    });
                }

                settings.SuccessPercent = (byte)Math.Clamp(c.SuccessPercent, 1, 100);
                settings.SuccessPercentageAdditionForLuck = Math.Clamp(c.LuckBonusPercent, 0, 100);
                applied++;
            }

            result.AppliedCount += applied;
        }

        if (options.ImportEventSchedules && preview.EventSchedules.Count > 0)
        {
            var timetable = ReadPlugInConfig<TimetableConfiguration>(config, typeof(TimetablePlugIn).GUID) ?? new TimetableConfiguration();
            foreach (var ev in preview.EventSchedules.Where(e => !e.IsBonusWindow))
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

            // The start times read from the server's files are the events' real timetables; they only went to the
            // timetable display, so the events kept HNMO's times (every 4 hours).
            foreach (var ev in preview.EventSchedules.Where(e => e.HasRealTimes))
            {
                var times = ConvertTimes(ParseTimes(ev.ScheduleTimes), options);
                if (times.Count == 0)
                {
                    continue;
                }

                switch (ev.EventId)
                {
                    case "imported-devil-square":
                        SetTimetable(persistenceContext, config, typeof(DevilSquareStartPlugIn).GUID, DevilSquareStartConfiguration.Default, times);
                        break;
                    case "imported-chaos-castle":
                        SetTimetable(persistenceContext, config, typeof(ChaosCastleStartPlugIn).GUID, ChaosCastleStartConfiguration.Default, times);
                        break;
                    case "imported-blood-castle":
                        SetTimetable(persistenceContext, config, typeof(BloodCastleStartPlugIn).GUID, BloodCastleStartConfiguration.Default, times);
                        break;
                    default:
                        continue;
                }

                result.SummaryLines.Add($"{ev.Name} starts at {string.Join(", ", times.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture)))}.");
            }

            // The bonus windows ("happy hours") go to the bonus events, which raise EXP and drops while they run. Those
            // without a map are server-wide (MapNumber null): they went to the timetable, which only shows them, so the
            // servers' four daily two-hour windows of EXP x1.5 did nothing.
            var bonusEvents = preview.EventSchedules.Where(e => e.IsBonusWindow).ToList();
            if (bonusEvents.Count > 0)
            {
                var bonusCfg = ReadPlugInConfig<BonusEventsConfiguration>(config, typeof(BonusEventsPlugIn).GUID) ?? new BonusEventsConfiguration();
                bonusCfg.Enabled = true;
                if (!string.IsNullOrWhiteSpace(options.SourceTimeZone) && !string.IsNullOrWhiteSpace(options.TargetTimeZone))
                {
                    bonusCfg.TimeZone = options.TargetTimeZone;
                }
                foreach (var be in bonusEvents)
                {
                    // ItemDropBonus is added to the base chance in percentage points (50 % + 15 = 65 %), so it is
                    // 15 / 50 = +30 % of the drops; it was taken as +15 %.
                    var dropBonus = preview.Rates?.ItemDropPercent is int basePercent and > 0
                        ? (int)Math.Round(be.DropBonusPercent * 100.0 / basePercent)
                        : be.DropBonusPercent;

                    var startTime = TimeOnly.TryParseExact(be.ScheduleTimes, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart)
                        ? ConvertTime(parsedStart, options, bonusCfg.TimeZone) // to the time zone the bonus events run in
                        : (Time: new TimeOnly(20, 0), DayShift: 0);

                    // Importing again replaces the window it imported before instead of adding it a second time, which
                    // stacked the bonuses.
                    bonusCfg.Events.RemoveAll(e => e.Id.Equals(be.EventId, StringComparison.OrdinalIgnoreCase));
                    bonusCfg.Events.Add(new BonusEventEntry
                    {
                        Id = be.EventId,
                        NameEn = be.Name,
                        NameVi = be.Name,
                        MapNumber = be.MapNumber,
                        StartTimeSydney = startTime.Time.ToString("HH:mm", CultureInfo.InvariantCulture),
                        DurationMinutes = be.DurationMinutes,
                        ExpBonusPercent = be.ExpBonusPercent,
                        ItemDropBonusPercent = dropBonus,
                        ZenDropBonusPercent = 0,
                        DaysOfWeek = be.DayOfWeek is >= 0 and <= 6 ? [(DayOfWeek)(((be.DayOfWeek + startTime.DayShift) % 7 + 7) % 7)] : [],
                    });
                }

                WritePlugInConfig(persistenceContext, config, typeof(BonusEventsPlugIn).GUID, bonusCfg, activate: true);
            }

            result.AppliedCount += preview.EventSchedules.Count;
        }

        if (options.ImportMonsterSpawns && preview.Spawns.Count > 0)
        {
            var duplicates = 0;
            var eventMapSpawns = 0;
            var matchedSpawns = new HashSet<MonsterSpawnArea>();
            var createdSpawns = new HashSet<MonsterSpawnArea>();
            foreach (var spawn in preview.Spawns)
            {
                if (!mapsByNum.TryGetValue(spawn.MapNumber, out var map)
                    || !monstersByNum.TryGetValue(spawn.MonsterNumber, out var monster))
                {
                    continue;
                }

                // HNMO's maps already have their monsters and NPCs, taken from the same kind of data: the imported ones
                // were added next to them, so every field had its monsters twice and every NPC stood twice. A spawn of
                // the same monster at (or near) a place the map already has one is the same spawn and is left out.
                // Devil Square, Blood Castle, Chaos Castle, Illusion Temple: HNMO's event maps have their monsters come
                // in waves when the event runs. The server lists them as normal spawns, which would stand there all day.
                // (A field map has some event spawns too, the golden monsters of the invasions and the wandering
                // merchants, so a map counts as an event map when most of its spawns are the event's.)
                if (IsEventMap(map, createdSpawns))
                {
                    eventMapSpawns++;
                    continue;
                }

                // Each spawn the map had stands for one imported spawn only: two guards a cell apart are two spawns.
                if (map.MonsterSpawns.FirstOrDefault(existing => !matchedSpawns.Contains(existing) && !createdSpawns.Contains(existing) && IsSameSpawn(existing, spawn)) is { } same)
                {
                    matchedSpawns.Add(same);
                    duplicates++;
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
                area.SpawnTrigger = SpawnTrigger.Automatic;

                // The servers count directions 0-7 starting west (-1: any), HNMO 1-8 (0: undefined).
                area.Direction = spawn.Direction is >= 0 and <= 7 ? (Direction)(spawn.Direction + 1) : Direction.Undefined;
                map.MonsterSpawns.Add(area);
                createdSpawns.Add(area);
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

            if (duplicates > 0)
            {
                result.SkippedCount += duplicates;
                result.SummaryLines.Add($"{duplicates} spawns skipped: the map already has the same monster or NPC there.");
            }

            if (eventMapSpawns > 0)
            {
                result.SkippedCount += eventMapSpawns;
                result.SummaryLines.Add($"{eventMapSpawns} spawns skipped: they are on event maps (Devil Square, Blood Castle ...), whose monsters come with the event.");
            }
        }

        // MonsterList.xml: the monsters' stats where they differ from HNMO's, each changed monster backed up.
        foreach (var stats in preview.MonsterStats.Where(_ => options.ImportMonsterStats))
        {
            if (!monstersByNum.TryGetValue(stats.Number, out var monster))
            {
                continue;
            }

            var backup = new BackupMonsterStats
            {
                Number = monster.Number,
                MoveRange = monster.MoveRange,
                AttackRange = monster.AttackRange,
                ViewRange = monster.ViewRange,
                MoveDelay = monster.MoveDelay,
                AttackDelay = monster.AttackDelay,
                RespawnDelay = monster.RespawnDelay,
            };
            foreach (var (stat, value) in MonsterStatTable(stats))
            {
                var attribute = monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == stat.Id);
                if (attribute is not null)
                {
                    backup.Attributes[stat.Id] = attribute.Value;
                    attribute.Value = value;
                }
                else if (config.Attributes.FirstOrDefault(a => a.Id == stat.Id) is { } definition)
                {
                    attribute = persistenceContext.CreateNew<MonsterAttribute>();
                    attribute.AttributeDefinition = definition;
                    attribute.Value = value;
                    monster.Attributes.Add(attribute);
                }
            }

            monster.MoveRange = (byte)Math.Clamp(stats.MoveRange, 0, 255);
            monster.AttackRange = (byte)Math.Clamp(stats.AttackRange, 0, 255);
            monster.ViewRange = (short)Math.Clamp(stats.ViewRange, 0, short.MaxValue);
            if (stats.MoveDelayMilliseconds > 0)
            {
                monster.MoveDelay = TimeSpan.FromMilliseconds(stats.MoveDelayMilliseconds);
            }

            if (stats.AttackDelayMilliseconds > 0)
            {
                monster.AttackDelay = TimeSpan.FromMilliseconds(stats.AttackDelayMilliseconds);
            }

            if (stats.RespawnSeconds > 0)
            {
                monster.RespawnDelay = TimeSpan.FromSeconds(stats.RespawnSeconds);
            }

            snapshot.PreviousMonsterStats.Add(backup);
            result.AppliedCount++;
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

    private static bool IsEventMap(GameMapDefinition map, HashSet<MonsterSpawnArea> createdSpawns)
    {
        var own = map.MonsterSpawns.Where(s => !createdSpawns.Contains(s) && s.SpawnTrigger != SpawnTrigger.Wandering).ToList();
        return own.Count > 0 && own.Count(s => s.SpawnTrigger != SpawnTrigger.Automatic) * 2 > own.Count;
    }

    /// <summary>Finds the first free place of a store grid an item of the size fits in, and takes it.</summary>
    /// <returns>The slot (row * 8 + column) of the item's top left cell, or <see langword="null"/> if it fits nowhere.</returns>
    private static byte? FindShopSlot(bool[,] occupied, int width, int height)
    {
        for (var row = 0; row + height <= ShopRows; row++)
        {
            for (var column = 0; column + width <= ShopColumns; column++)
            {
                var free = true;
                for (var r = row; r < row + height && free; r++)
                {
                    for (var c = column; c < column + width && free; c++)
                    {
                        free = !occupied[r, c];
                    }
                }

                if (!free)
                {
                    continue;
                }

                for (var r = row; r < row + height; r++)
                {
                    for (var c = column; c < column + width; c++)
                    {
                        occupied[r, c] = true;
                    }
                }

                return (byte)((row * ShopColumns) + column);
            }
        }

        return null;
    }

    /// <summary>
    /// Tells whether a map's spawn is the imported one already: the same monster, in an area that overlaps the imported
    /// one (each widened by 3 cells, since the servers' spots and HNMO's own differ by a cell or two).
    /// </summary>
    private static bool IsSameSpawn(MonsterSpawnArea existing, LegacySpawnEntry spawn)
    {
        const int tolerance = 3;
        return existing.MonsterDefinition?.Number == spawn.MonsterNumber
            && existing.X1 - tolerance <= Math.Max(spawn.X1, spawn.X2) && spawn.X1 - tolerance <= existing.X2
            && existing.Y1 - tolerance <= Math.Max(spawn.Y1, spawn.Y2) && spawn.Y1 - tolerance <= existing.Y2;
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
            if (snapshot.PreviousCommonItemDropChance is { } itemChance && CommonDropGroup(config, SpecialItemType.RandomItem) is { } itemGroup)
            {
                itemGroup.Chance = itemChance;
            }

            if (snapshot.PreviousExcellentItemDropChance is { } excChance && CommonDropGroup(config, SpecialItemType.Excellent) is { } excGroup)
            {
                excGroup.Chance = excChance;
            }

            config.MaximumLevel = snapshot.PreviousMaximumLevel ?? config.MaximumLevel;
            config.MaximumMasterLevel = snapshot.PreviousMaximumMasterLevel ?? config.MaximumMasterLevel;
            if (snapshot.PreviousLuckAddChance is { } luckChance && LuckOptionDefinition(config) is { } luckDefinition)
            {
                luckDefinition.AddChance = luckChance;
            }

            foreach (var (mapNumber, multiplier) in snapshot.PreviousMapExpMultipliers)
            {
                foreach (var map in config.Maps.Where(m => m.Number == mapNumber))
                {
                    map.ExpMultiplier = multiplier;
                }
            }
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

        if (snapshot.PreviousCraftingRates.Count > 0)
        {
            var craftings = config.Monsters.SelectMany(m => m.ItemCraftings)
                .Where(c => c.SimpleCraftingSettings is not null)
                .GroupBy(c => c.Number)
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var rate in snapshot.PreviousCraftingRates)
            {
                if (craftings.TryGetValue(rate.CraftingNumber, out var crafting))
                {
                    crafting.SimpleCraftingSettings!.SuccessPercent = rate.SuccessPercent;
                    crafting.SimpleCraftingSettings.SuccessPercentageAdditionForLuck = rate.LuckBonusPercent;
                    crafting.SimpleCraftingSettings.MaximumSuccessPercent = rate.MaximumSuccessPercent;
                }
            }
        }

        var allItems = config.Items.ToDictionary(i => $"{i.Group}:{i.Number}", StringComparer.Ordinal);
        foreach (var previous in snapshot.PreviousEventRewards)
        {
            var group = config.MiniGameDefinitions
                .Where(g => (int)g.Type == previous.MiniGameType && g.GameLevel == previous.Level)
                .SelectMany(g => g.Rewards)
                .FirstOrDefault(r => r.RewardType == MiniGameRewardType.ItemDrop && r.ItemReward is { ItemType: SpecialItemType.None or SpecialItemType.RandomItem })?.ItemReward;
            if (group is null)
            {
                continue;
            }

            group.PossibleItems.Clear();
            foreach (var key in previous.ItemKeys)
            {
                if (allItems.TryGetValue(key, out var item))
                {
                    group.PossibleItems.Add(item);
                }
            }

            group.Chance = previous.Chance;
        }

        foreach (var previous in snapshot.PreviousMonsterStats)
        {
            if (config.Monsters.FirstOrDefault(m => m.Number == previous.Number) is not { } monster)
            {
                continue;
            }

            monster.MoveRange = previous.MoveRange;
            monster.AttackRange = previous.AttackRange;
            monster.ViewRange = previous.ViewRange;
            monster.MoveDelay = previous.MoveDelay;
            monster.AttackDelay = previous.AttackDelay;
            monster.RespawnDelay = previous.RespawnDelay;
            foreach (var stat in MonsterStatTable(new LegacyMonsterStats()).Select(s => s.Stat.Id))
            {
                var attribute = monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == stat);
                if (attribute is null)
                {
                    continue;
                }

                if (previous.Attributes.TryGetValue(stat, out var value))
                {
                    attribute.Value = value;
                }
                else
                {
                    // The import added it.
                    monster.Attributes.Remove(attribute);
                    await persistenceContext.DeleteAsync(attribute).ConfigureAwait(false);
                }
            }
        }

        var boxTags = snapshot.CreatedBoxContentTags.ToHashSet(StringComparer.Ordinal);
        if (boxTags.Count > 0)
        {
            foreach (var item in config.Items)
            {
                foreach (var content in item.DropItems.Where(g => boxTags.Contains(g.Description.ValueInNeutralLanguage)).ToList())
                {
                    item.DropItems.Remove(content);
                    await persistenceContext.DeleteAsync(content).ConfigureAwait(false);
                }
            }
        }

        var dropTags = snapshot.CreatedDropGroupTags.ToHashSet(StringComparer.Ordinal);
        foreach (var toRemove in config.DropItemGroups.Where(g => dropTags.Contains(g.Description.ValueInNeutralLanguage)).ToList())
        {
            config.DropItemGroups.Remove(toRemove);
            foreach (var monster in config.Monsters.Where(m => m.DropItemGroups.Contains(toRemove)))
            {
                monster.DropItemGroups.Remove(toRemove);
            }

            foreach (var map in config.Maps.Where(m => m.DropItemGroups.Contains(toRemove)))
            {
                map.DropItemGroups.Remove(toRemove);
            }

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
                AddLegacyOptions(persistenceContext, restored, prevItem.HasLuck, prevItem.OptionLevel, prevItem.ExcellentBits);
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
        foreach (var decoded in LegacyFileParsers.ParseLegacyBinaryStorage(blob, LegacyFileParsers.ItemSlotSizeOf(blob)))
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
            AddLegacyOptions(context, item, decoded.HasLuck, decoded.OptionLevel, decoded.ExcellentFlags);
            storage.Items.Add(item);
        }
    }

    /// <summary>
    /// Gives an imported item its luck, its normal option (+4 .. +28) and its excellent options, as the server stored or
    /// sold it; only the level, durability and skill were taken over. Options the item cannot have are left out.
    /// </summary>
    private static void AddLegacyOptions(IContext context, Item item, bool luck, byte optionLevel, byte excellentBits)
    {
        var possible = item.Definition!.PossibleItemOptions.SelectMany(o => o.PossibleOptions).ToList();
        if (luck && possible.FirstOrDefault(o => o.OptionType == ItemOptionTypes.Luck) is { } luckOption)
        {
            var link = context.CreateNew<ItemOptionLink>();
            link.ItemOption = luckOption;
            item.ItemOptions.Add(link);
        }

        if (optionLevel > 0 && possible.FirstOrDefault(o => o.OptionType == ItemOptionTypes.Option) is { } normalOption)
        {
            var link = context.CreateNew<ItemOptionLink>();
            link.ItemOption = normalOption;
            link.Level = Math.Min(optionLevel, (byte)7);
            item.ItemOptions.Add(link);
        }

        if (excellentBits != 0)
        {
            foreach (var excellent in possible.Where(o => o.OptionType == ItemOptionTypes.Excellent && o.Number is >= 1 and <= 6))
            {
                if ((excellentBits & (1 << (excellent.Number - 1))) != 0)
                {
                    var link = context.CreateNew<ItemOptionLink>();
                    link.ItemOption = excellent;
                    item.ItemOptions.Add(link);
                }
            }
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

    /// <summary>
    /// The common drop group of a kind (GameConfigurationInitializerBase.AddItemDropItemGroups): for every monster, no
    /// item list of its own (the random and the excellent items).
    /// </summary>
    private static DropItemGroup? CommonDropGroup(GameConfiguration config, SpecialItemType type)
        => config.DropItemGroups.FirstOrDefault(g => g.ItemType == type && g.Monster is null && g.PossibleItems.Count == 0);

    /// <summary>The attributes of a monster the server's MonsterList.xml sets, with their values.</summary>
    private static IEnumerable<(AttributeDefinition Stat, float Value)> MonsterStatTable(LegacyMonsterStats stats)
    {
        yield return (Stats.Level, stats.Level);
        yield return (Stats.MaximumHealth, stats.Health);
        yield return (Stats.MinimumPhysBaseDmg, stats.DamageMin);
        yield return (Stats.MaximumPhysBaseDmg, stats.DamageMax);
        yield return (Stats.DefenseBase, stats.Defense);
        yield return (Stats.AttackRatePvm, stats.AttackRate);
        yield return (Stats.DefenseRatePvm, stats.DefenseRate);
    }

    private static bool MonsterStatsDiffer(MonsterDefinition monster, LegacyMonsterStats stats)
        => MonsterStatTable(stats).Any(s => Math.Abs((monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == s.Stat.Id)?.Value ?? 0) - s.Value) > 0.001f)
            || monster.MoveRange != stats.MoveRange
            || monster.AttackRange != stats.AttackRange
            || monster.ViewRange != stats.ViewRange
            || (stats.MoveDelayMilliseconds > 0 && (int)monster.MoveDelay.TotalMilliseconds != stats.MoveDelayMilliseconds)
            || (stats.AttackDelayMilliseconds > 0 && (int)monster.AttackDelay.TotalMilliseconds != stats.AttackDelayMilliseconds)
            || (stats.RespawnSeconds > 0 && (int)monster.RespawnDelay.TotalSeconds != stats.RespawnSeconds);

    /// <summary>
    /// Converts a time of day of the legacy server's time zone to the target time zone (both from the options; no
    /// conversion when the source is not given), as of today. Returns the converted time and how many days it moved
    /// (-1, 0 or +1), for events on one weekday.
    /// </summary>
    private static (TimeOnly Time, int DayShift) ConvertTime(TimeOnly time, LegacyImportOptions options, string? targetZoneId = null)
    {
        if (string.IsNullOrWhiteSpace(options.SourceTimeZone))
        {
            return (time, 0);
        }

        var source = HnmoStateStore.ResolveTimeZone(options.SourceTimeZone);
        var target = HnmoStateStore.ResolveTimeZone(string.IsNullOrWhiteSpace(targetZoneId) ? options.TargetTimeZone : targetZoneId);
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, source).Date;
        var sourceTime = DateTime.SpecifyKind(today + time.ToTimeSpan(), DateTimeKind.Unspecified);
        var targetTime = TimeZoneInfo.ConvertTime(sourceTime, source, target);
        return (TimeOnly.FromDateTime(targetTime), (targetTime.Date - today).Days);
    }

    private static List<TimeOnly> ConvertTimes(IEnumerable<TimeOnly> times, LegacyImportOptions options)
        => times.Select(t => ConvertTime(t, options).Time).Distinct().Order().ToList();

    private static List<TimeOnly> ParseTimes(string scheduleTimes)
        => scheduleTimes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : (TimeOnly?)null)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .Distinct()
            .Order()
            .ToList();

    /// <summary>Sets the timetable of a mini game's start plug-in, keeping its other settings and its on/off state.</summary>
    private static void SetTimetable<T>(IContext context, GameConfiguration config, Guid typeId, T defaults, List<TimeOnly> times)
        where T : PeriodicTaskConfiguration
    {
        var settings = ReadPlugInConfig<T>(config, typeId) ?? defaults;
        settings.Timetable = times;
        WritePlugInConfig(context, config, typeId, settings, activate: config.PlugInConfigurations.All(p => p.TypeId != typeId));
    }

    /// <summary>The option definition of luck, whose AddChance is the chance that a dropped item has luck.</summary>
    private static ItemOptionDefinition? LuckOptionDefinition(GameConfiguration config)
        => config.ItemOptions.FirstOrDefault(o => o.AddsRandomly && o.PossibleOptions.Count > 0 && o.PossibleOptions.All(p => p.OptionType == ItemOptionTypes.Luck));

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
            // With the plug-ins' own JSON converters (LocalizedString of the event messages ...), as the server reads them.
            return entry.GetConfiguration<T>(null);
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

        entry.SetConfiguration(model, null);
        if (activate)
        {
            entry.IsActive = true;
        }
    }
}
