// <copyright file="LegacyFileParsers.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ServerImporter;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Pure HNMO-written parsers for structured INI, XML, and tabular text configuration folders used by legacy servers.
/// </summary>
public static class LegacyFileParsers
{
    private static readonly char[] WhitespaceSeparators = [' ', '\t'];

    /// <summary>The file name of the item bag a monster drops: "Monster_(275)_Kundun.xml" is monster 275's.</summary>
    private static readonly Regex MonsterBagName = new(@"^Monster_\((\d+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Detects whether a legacy server folder targets Season 6 or Season 9 based on structural markers.
    /// </summary>
    /// <param name="rootFolder">The server root directory.</param>
    /// <returns>The detected version and diagnostic markers.</returns>
    public static (LegacyServerVersion Version, List<string> Signals) DetectVersion(string rootFolder)
    {
        var signals = new List<string>();
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
        {
            return (LegacyServerVersion.Unknown, signals);
        }

        var hasBattleCore = Directory.GetDirectories(rootFolder, "*BattleCore*", SearchOption.AllDirectories).Length > 0;
        var hasMuun = Directory.GetFiles(rootFolder, "*MuunInfo*.xml", SearchOption.AllDirectories).Length > 0;
        var hasPentagram = Directory.GetFiles(rootFolder, "*PentagramOption*.xml", SearchOption.AllDirectories).Length > 0;
        var hasArca = Directory.GetFiles(rootFolder, "*ArcaBattle*.xml", SearchOption.AllDirectories).Length > 0;
        var hasTormented = Directory.GetFiles(rootFolder, "*TormentedSquare*.xml", SearchOption.AllDirectories).Length > 0;

        if (hasBattleCore)
        {
            signals.Add("BattleCore server files detected (Season 9+ feature).");
        }

        if (hasMuun)
        {
            signals.Add("Companion (Muun) configuration XML detected (Season 9+ feature).");
        }

        if (hasPentagram)
        {
            signals.Add("Elemental Pentagram XML detected (Season 9+ feature).");
        }

        if (hasArca || hasTormented)
        {
            signals.Add("Arca / Tormented Square event XML detected (Season 9+ feature).");
        }

        var commonIni = FindFiles(rootFolder, "*Common.ini").Concat(FindFiles(rootFolder, "CommonServer.cfg")).ToList();
        foreach (var file in commonIni)
        {
            var kv = ReadIniKeyValues(file);
            if (kv.TryGetValue("MaxMasterLevel", out var maxMl)
                && int.TryParse(maxMl, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ml)
                && ml > 200)
            {
                signals.Add($"MaxMasterLevel={ml} (>200 indicates Season 8/9+ progression).");
            }
        }

        // Muun pets alone do not make a Season 9 server: IGCN's Season 6 Episode 3 suite has them too.
        if (hasBattleCore || hasPentagram || hasArca || hasTormented
            || signals.Any(s => s.Contains(">200", StringComparison.Ordinal)))
        {
            return (LegacyServerVersion.Season9, signals);
        }

        var hasS6Data = FindFiles(rootFolder, "*Common.ini").Any()
            || FindFiles(rootFolder, "CommonServer.cfg").Any()
            || FindFiles(rootFolder, "*ShopList*.xml").Any()
            || FindFiles(rootFolder, "*MonsterSetBase*.txt").Any()
            || FindFiles(rootFolder, "*MonsterSpawn*.xml").Any();
        if (hasS6Data)
        {
            signals.Add("Standard Season 6 INI/XML/spawn configuration files detected.");
            return (LegacyServerVersion.Season6, signals);
        }

        return (LegacyServerVersion.Unknown, signals);
    }

    /// <summary>Parses EXP, Master EXP, Item Drop %, and Zen multiplier settings.</summary>
    /// <param name="rootFolder">The server root directory.</param>
    /// <returns>The parsed rate settings.</returns>
    public static LegacyParsedRates ParseRates(string rootFolder)
    {
        var rates = new LegacyParsedRates();
        if (!Directory.Exists(rootFolder))
        {
            return rates;
        }

        foreach (var file in FindFiles(rootFolder, "*ExpSystem*.xml"))
        {
            try
            {
                var doc = XDocument.Load(file);
                var staticExp = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("StaticExp", StringComparison.OrdinalIgnoreCase));
                if (staticExp is not null)
                {
                    if (TryReadFloatAttr(staticExp, "Normal", out var normal))
                    {
                        rates.ExperienceRate = Math.Max(0.1f, normal);
                    }

                    if (TryReadFloatAttr(staticExp, "Master", out var master))
                    {
                        rates.MasterExperienceRate = Math.Max(0.1f, master);
                    }
                }
            }
            catch
            {
                // Ignore malformed optional file; reported only when critical
            }
        }

        foreach (var cfg in FindFiles(rootFolder, "CommonServer.cfg").Concat(FindFiles(rootFolder, "*Common.ini")))
        {
            var kv = ReadIniKeyValues(cfg);
            if (!rates.ExperienceRate.HasValue && kv.TryGetValue("AddExperience", out var expStr)
                && float.TryParse(expStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var expRate))
            {
                rates.ExperienceRate = Math.Max(0.1f, expRate);
            }

            if (kv.TryGetValue("ItemDropPer", out var dropStr)
                && int.TryParse(dropStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dropPct))
            {
                rates.ItemDropPercent = Math.Clamp(dropPct, 0, 100);
            }

            if (kv.TryGetValue("ExcItemDropPer", out var excStr)
                && int.TryParse(excStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var excRate))
            {
                rates.ExcellentItemDropPerTenThousand = Math.Clamp(excRate, 0, 10000);
            }

            if (kv.TryGetValue("ZenDropMultipler", out var zenStr) || kv.TryGetValue("ZenDropMultiplier", out zenStr))
            {
                if (double.TryParse(zenStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var zenMult))
                {
                    rates.ZenMultiplier = Math.Max(0.0, zenMult);
                }
            }

            rates.SoulSuccessPercent = ReadPercent(kv, "UseSoulRate") ?? rates.SoulSuccessPercent;
            rates.SoulWithLuckSuccessPercent = ReadPercent(kv, "UseSoulWithLuckRate") ?? rates.SoulWithLuckSuccessPercent;
            rates.LifeSuccessPercent = ReadPercent(kv, "UseLifeRate") ?? rates.LifeSuccessPercent;
        }

        return rates;
    }

    /// <summary>Parses DropManager-style XML files into drop rules.</summary>
    /// <param name="rootFolder">The server root directory.</param>
    /// <returns>The parsed drop rules.</returns>
    public static List<LegacyDropRule> ParseDropLists(string rootFolder)
    {
        var results = new List<LegacyDropRule>();
        if (!Directory.Exists(rootFolder))
        {
            return results;
        }

        foreach (var file in FindFiles(rootFolder, "*DropManager*.xml"))
        {
            try
            {
                var doc = XDocument.Load(file);
                foreach (var monster in doc.Descendants().Where(e => e.Name.LocalName.Equals("Monster", StringComparison.OrdinalIgnoreCase)))
                {
                    var monsterNum = ReadIntAttr(monster, "Index", -1);
                    var minLv = ReadIntAttr(monster, "MonsterMinLevel", 0);
                    var maxLv = ReadIntAttr(monster, "MonsterMaxLevel", 255);
                    var mapNum = ReadIntAttr(monster, "MapNumber", -1);
                    var sectionRate = ReadIntAttr(monster, "ItemRate", 10000);
                    var items = monster.Elements().Where(e => e.Name.LocalName.Equals("Item", StringComparison.OrdinalIgnoreCase)).ToList();
                    var totalWeight = Math.Max(1.0, items.Sum(i => Math.Max(1, ReadIntAttr(i, "DropRate", 1))));

                    foreach (var item in items)
                    {
                        var weight = Math.Max(1, ReadIntAttr(item, "DropRate", 1));
                        var effectiveChance = Math.Clamp((sectionRate / 10000.0) * (weight / totalWeight), 0.0001, 1.0);
                        if (TryCreateDropRule(item, monsterNum, minLv, maxLv, mapNum, effectiveChance, forceAncient: false, out var rule))
                        {
                            results.Add(rule);
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed optional XML file
            }
        }

        ParseSetItemDropManagers(rootFolder, results);
        ParseItemBags(rootFolder, results);

        return results;
    }

    /// <summary>Parses ShopList XML and referenced merchant shop XML or TXT files.</summary>
    /// <param name="rootFolder">The server root directory.</param>
    /// <returns>The parsed merchant definitions.</returns>
    public static List<LegacyShopDefinition> ParseShops(string rootFolder)
    {
        var shops = new List<LegacyShopDefinition>();
        if (!Directory.Exists(rootFolder))
        {
            return shops;
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var listFile in FindFiles(rootFolder, "*ShopList*.xml"))
        {
            var baseDir = Path.GetDirectoryName(listFile)!;
            try
            {
                var doc = XDocument.Load(listFile);
                foreach (var shopEl in doc.Descendants().Where(e => e.Name.LocalName.Equals("Shop", StringComparison.OrdinalIgnoreCase)))
                {
                    var npcIndex = ReadIntAttr(shopEl, "NPCIndex", -1);
                    var mapNum = ReadIntAttr(shopEl, "MapNumber", 0);
                    var fileName = shopEl.Attribute("FileName")?.Value?.Trim();
                    // GMShop="1" is the game masters' test shop: it sits on an NPC of the normal shops (Lumen, 255).
                    if (npcIndex < 0 || string.IsNullOrWhiteSpace(fileName) || ReadIntAttr(shopEl, "GMShop", 0) != 0)
                    {
                        continue;
                    }

                    var dedupe = $"{npcIndex}:{fileName}";
                    if (!seenKeys.Add(dedupe))
                    {
                        continue;
                    }

                    var shopPath = ResolveShopFile(rootFolder, baseDir, fileName);
                    if (shopPath is null)
                    {
                        continue;
                    }

                    var items = shopPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                        ? ParseShopXml(shopPath)
                        : ParseShopTxt(shopPath);

                    shops.Add(new LegacyShopDefinition
                    {
                        NpcNumber = npcIndex,
                        MapNumber = mapNum,
                        Items = items,
                    });
                }
            }
            catch
            {
                // Ignore malformed ShopList
            }
        }

        return shops;
    }

    /// <summary>Parses classic and Season 9 event configurations into schedulable entries.</summary>
    /// <param name="rootFolder">The server root directory.</param>
    /// <returns>The parsed event schedule entries.</returns>
    public static List<LegacyEventScheduleEntry> ParseEventSchedules(string rootFolder)
    {
        var events = new List<LegacyEventScheduleEntry>();
        if (!Directory.Exists(rootFolder))
        {
            return events;
        }

        foreach (var file in FindFiles(rootFolder, "*BonusEvent*.xml"))
        {
            try
            {
                var doc = XDocument.Load(file);
                if (doc.Root is not null && ReadIntAttr(doc.Root, "Enable", 1) == 0)
                {
                    continue;
                }

                var idx = 0;
                foreach (var ev in doc.Descendants().Where(e => e.Name.LocalName.Equals("Event", StringComparison.OrdinalIgnoreCase)))
                {
                    idx++;
                    var enabled = ReadIntAttr(ev, "Enable", 1) != 0;
                    if (!enabled)
                    {
                        continue;
                    }

                    var startH = ReadIntAttr(ev, "StartHour", 20);
                    var startM = ReadIntAttr(ev, "StartMinute", 0);
                    var endH = ReadIntAttr(ev, "EndHour", -1);
                    var mapNum = ReadIntAttr(ev, "MapNumber", -1);
                    var expBonus = TryReadFloatAttr(ev, "ExpMultiplier", out var expMult)
                        ? Math.Max(0, (int)Math.Round((expMult - 1.0f) * 100.0f))
                        : ReadIntAttr(ev, "ExpBonus", 20);
                    var dropBonus = ev.Attribute("ItemDropBonus") is not null
                        ? ReadIntAttr(ev, "ItemDropBonus", 15)
                        : ReadIntAttr(ev, "DropBonus", 15);
                    var windowText = endH >= 0 ? $"Window {startH:D2}:{startM:D2}-{endH:D2}:{startM:D2}" : string.Empty;

                    // The window runs from StartHour to EndHour (across midnight when it ends earlier); it was taken as
                    // one hour although the servers' windows are two.
                    var duration = endH >= 0 ? ((((endH - startH) * 60) % 1440) + 1440) % 1440 : 60;
                    events.Add(new LegacyEventScheduleEntry
                    {
                        EventId = $"imported-bonus-{idx}",
                        Name = ev.Attribute("Name")?.Value?.Trim() is { Length: > 0 } n ? n : $"Bonus Event {idx}",
                        IsSeason9Only = false,
                        MapNumber = mapNum >= 0 ? (short)mapNum : null,
                        ScheduleTimes = $"{startH:D2}:{startM:D2}",
                        ExpBonusPercent = expBonus,
                        DropBonusPercent = dropBonus,
                        RewardSummary = windowText,
                        IsBonusWindow = true,
                        DurationMinutes = duration == 0 ? 1440 : duration,
                        DayOfWeek = ReadIntAttr(ev, "DayOfWeek", -1),
                    });
                }
            }
            catch
            {
                // Ignore malformed XML
            }
        }

        AddStandardEventSchedule(
            rootFolder,
            events,
            "*BloodCastle*.xml",
            "*BloodCastle*.ini",
            "imported-blood-castle",
            "Blood Castle",
            "00:30, 02:30, 04:30, 06:30, 08:30, 10:30, 12:30, 14:30, 16:30, 18:30, 20:30, 22:30",
            "Jewel of Chaos / Jewel of Bless");

        AddStandardEventSchedule(
            rootFolder,
            events,
            "*DevilSquare*.xml",
            "*DevilSquare*.ini",
            "imported-devil-square",
            "Devil Square",
            "01:00, 05:00, 09:00, 13:00, 17:00, 21:00",
            "Bonus EXP & Zen ranking");

        AddStandardEventSchedule(
            rootFolder,
            events,
            "*ChaosCastle*.xml",
            "*ChaosCastle*.ini",
            "imported-chaos-castle",
            "Chaos Castle",
            "01:30, 05:30, 09:30, 13:30, 17:30, 21:30",
            "Jewels / Ancient chance",
            excludePattern: "Survival");

        ParseInvasionSchedules(rootFolder, events);

        var s9Events = new (string Pattern, string Label)[]
        {
            ("*ArcaBattle*.xml", "Arca Battle"),
            ("*AcheronGuardian*.xml", "Acheron Guardian"),
            ("*ChaosCastle_Survival*.xml", "Chaos Castle Survival"),
            ("*TormentedSquare*.xml", "Tormented Square"),
            ("*IllusionTemple_Renewal*.xml", "Illusion Temple Renewal"),
        };

        foreach (var (pattern, label) in s9Events)
        {
            var matched = FindFiles(rootFolder, pattern).ToList();
            if (matched.Count > 0)
            {
                events.Add(new LegacyEventScheduleEntry
                {
                    EventId = $"s9-{label.ToLowerInvariant().Replace(' ', '-')}",
                    Name = label,
                    IsSeason9Only = true,
                    ScheduleTimes = ReadScheduleTimesFromXml(matched[0]) ?? "20:00",
                    RewardSummary = "Season 9 exclusive event",
                });
            }
        }

        return events;
    }

    /// <summary>Parses ChaosBox INI files for +10..+15 enhancement and Luck bonus percentages.</summary>
    /// <param name="rootFolder">The server root directory.</param>
    /// <returns>The parsed Chaos Machine rate overrides.</returns>
    public static List<LegacyChaosRateEntry> ParseChaosMixRates(string rootFolder)
    {
        var results = new List<LegacyChaosRateEntry>();
        if (!Directory.Exists(rootFolder))
        {
            return results;
        }

        foreach (var file in FindFiles(rootFolder, "*ChaosBox*.ini"))
        {
            var kv = ReadIniKeyValues(file);
            var luckAdd = kv.TryGetValue("MixAddLuckRate_Normal", out var lStr)
                && int.TryParse(lStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lv)
                ? Math.Clamp(lv, 0, 100)
                : 25;

            var mappings = new (string Key, string CraftPattern)[]
            {
                ("Mix10Rate_Normal", "+10"),
                ("Mix11Rate_Normal", "+11"),
                ("Mix12Rate_Normal", "+12"),
                ("Mix13Rate_Normal", "+13"),
                ("Mix14Rate_Normal", "+14"),
                ("Mix15Rate_Normal", "+15"),
            };

            foreach (var (key, pattern) in mappings)
            {
                if (kv.TryGetValue(key, out var raw)
                    && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pct))
                {
                    results.Add(new LegacyChaosRateEntry
                    {
                        Key = key,
                        CraftNamePattern = pattern,
                        SuccessPercent = Math.Clamp(pct, 1, 100),
                        LuckBonusPercent = luckAdd,
                    });
                }
            }
        }

        return results;
    }

    /// <summary>Parses classic tabular MonsterSetBase spawn text files.</summary>
    /// <param name="rootFolder">The server root directory.</param>
    /// <returns>The parsed spawn entries.</returns>
    public static List<LegacySpawnEntry> ParseMonsterSpawns(string rootFolder)
    {
        var spawns = new List<LegacySpawnEntry>();
        if (!Directory.Exists(rootFolder))
        {
            return spawns;
        }

        // A suite has the spawns of its normal game servers in MonsterSetBase.txt and those of its castle siege server in
        // MonsterSetBase_CS.txt, which is almost the same list: reading both put every monster in twice.
        var setBaseFiles = FindFiles(rootFolder, "MonsterSetBase*.txt").ToList();
        if (setBaseFiles.Any(f => !IsCastleSiegeFile(f)))
        {
            setBaseFiles.RemoveAll(IsCastleSiegeFile);
        }

        foreach (var file in setBaseFiles)
        {
            var currentGroup = -1;
            foreach (var rawLine in File.ReadLines(file))
            {
                var line = StripLineComment(rawLine).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.Equals("end", StringComparison.OrdinalIgnoreCase))
                {
                    currentGroup = -1;
                    continue;
                }

                var tokens = line.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 1 && int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupId))
                {
                    currentGroup = groupId;
                    continue;
                }

                if (currentGroup < 0 || tokens.Length < 6)
                {
                    continue;
                }

                if (!TryParseInt(tokens[0], out var monsterId)
                    || !TryParseInt(tokens[1], out var mapId)
                    || !TryParseInt(tokens[3], out var x1)
                    || !TryParseInt(tokens[4], out var y1))
                {
                    continue;
                }

                if (currentGroup is 1 or 3 && tokens.Length >= 9
                    && TryParseInt(tokens[5], out var x2)
                    && TryParseInt(tokens[6], out var y2)
                    && TryParseInt(tokens[7], out var dir)
                    && TryParseInt(tokens[8], out var count))
                {
                    spawns.Add(new LegacySpawnEntry
                    {
                        SpawnGroup = currentGroup,
                        MonsterNumber = monsterId,
                        MapNumber = mapId,
                        X1 = Math.Clamp(Math.Min(x1, x2), 0, 255),
                        Y1 = Math.Clamp(Math.Min(y1, y2), 0, 255),
                        X2 = Math.Clamp(Math.Max(x1, x2), 0, 255),
                        Y2 = Math.Clamp(Math.Max(y1, y2), 0, 255),
                        Direction = dir,
                        Quantity = Math.Max(1, count),
                    });
                }
                else
                {
                    // The third column (30 for nearly every monster, 0 for NPCs) is the monster's roaming distance,
                    // not a spawn area, so a single spot stays a single cell, as in HNMO's own map data.
                    TryParseInt(tokens[5], out var spawnDir);
                    spawns.Add(new LegacySpawnEntry
                    {
                        SpawnGroup = currentGroup,
                        MonsterNumber = monsterId,
                        MapNumber = mapId,
                        X1 = Math.Clamp(x1, 0, 255),
                        Y1 = Math.Clamp(y1, 0, 255),
                        X2 = Math.Clamp(x1, 0, 255),
                        Y2 = Math.Clamp(y1, 0, 255),
                        Direction = spawnDir,
                        Quantity = 1,
                    });
                }
            }
        }

        ParseXmlMonsterSpawns(rootFolder, spawns);

        return spawns;
    }

    /// <summary>
    /// The bytes per item slot of an inventory or vault blob. The Season 6 and Season 9 server databases this importer
    /// reads keep 32 bytes per slot (their scripts: inventories of 7,584 bytes = 237 slots, vaults of 7,680 = 240), of
    /// which the first 16 have the classic layout; older servers keep 16, in at most 240 slots (3,840 bytes). So a blob
    /// of whole 32-byte slots that is longer than any 16-byte one (7,000 bytes and more) has 32-byte slots.
    /// </summary>
    /// <param name="blob">The raw varbinary payload.</param>
    /// <returns>32 or 16.</returns>
    public static int ItemSlotSizeOf(byte[]? blob)
        => blob is { Length: >= 7000 } && blob.Length % 32 == 0 ? 32 : 16;

    /// <summary>Decodes classic 16-byte item slots from inventory or vault binary blobs.</summary>
    /// <param name="blob">The raw varbinary payload.</param>
    /// <param name="slotSize">Bytes per item slot (16 for classic/S6-S9 DB blobs).</param>
    /// <returns>The non-empty decoded items.</returns>
    public static List<LegacyDecodedItem> ParseLegacyBinaryStorage(byte[] blob, int slotSize = 16)
    {
        var list = new List<LegacyDecodedItem>();
        if (blob is null || blob.Length < slotSize)
        {
            return list;
        }

        var count = Math.Min(250, blob.Length / slotSize);
        for (var i = 0; i < count; i++)
        {
            var offset = i * slotSize;
            var b0 = blob[offset];
            if (b0 == 0xFF && blob[offset + 7] == 0xFF && blob[offset + 9] == 0xFF)
            {
                continue;
            }

            if (b0 == 0xFF && blob[offset + 1] == 0xFF)
            {
                continue;
            }

            var flags = blob[offset + 1];
            var durability = blob[offset + 2];
            var exc = blob[offset + 7];
            var ancient = blob[offset + 8];
            var catByte = blob[offset + 9];

            var group = (byte)((catByte >> 4) & 0x0F);
            var number = (short)(b0 | ((exc & 0x80) != 0 ? 0x100 : 0));
            var level = (byte)((flags >> 3) & 0x0F);
            var hasSkill = (flags & 0x80) != 0;
            var hasLuck = (flags & 0x04) != 0;
            var option = (byte)((flags & 0x03) | ((exc & 0x40) != 0 ? 0x04 : 0));

            list.Add(new LegacyDecodedItem
            {
                Slot = (byte)i,
                Group = group,
                Number = number,
                Level = level,
                Durability = durability,
                HasSkill = hasSkill,
                HasLuck = hasLuck,
                OptionLevel = option,
                ExcellentFlags = (byte)(exc & 0x3F),
                AncientFlags = ancient,
            });
        }

        return list;
    }

    private static void ParseSetItemDropManagers(string rootFolder, List<LegacyDropRule> results)
    {
        foreach (var file in FindFiles(rootFolder, "*SetItemDropManager*.xml"))
        {
            try
            {
                var doc = XDocument.Load(file);
                var globalRate = Math.Max(1, ReadIntAttr(doc.Root!, "DropUseRate", 10000));
                foreach (var ruleEl in doc.Descendants().Where(e => e.Name.LocalName.Equals("Rule", StringComparison.OrdinalIgnoreCase)))
                {
                    var monsterNum = ReadIntAttr(ruleEl, "MonsterIndex", -1);
                    var minLv = ReadIntAttr(ruleEl, "MonsterMinLevel", 0);
                    var maxLv = ReadIntAttr(ruleEl, "MonsterMaxLevel", 255);
                    var mapNum = ReadIntAttr(ruleEl, "MapNumber", -1);
                    // DropUseRate is the chance per 1,000,000 that an ancient item drops at all; each item's DropRate is
                    // its weight among the rule's items (it was read as n/10000 against the item weights).
                    var itemEls = ruleEl.Elements().Where(e => e.Name.LocalName.Equals("ItemDrop", StringComparison.OrdinalIgnoreCase)).ToList();
                    var totalWeight = Math.Max(1.0, itemEls.Sum(i => Math.Max(1, ReadIntAttr(i, "DropRate", 1))));
                    foreach (var itemEl in itemEls)
                    {
                        var dropRate = Math.Max(1, ReadIntAttr(itemEl, "DropRate", 1));
                        var chance = Math.Clamp(globalRate / 1_000_000.0 * (dropRate / totalWeight), 0.000001, 1.0);
                        if (TryCreateDropRule(itemEl, monsterNum, minLv, maxLv, mapNum, chance, forceAncient: true, out var rule))
                        {
                            results.Add(rule);
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed optional XML file
            }
        }
    }

    private static void ParseItemBags(string rootFolder, List<LegacyDropRule> results)
    {
        foreach (var file in FindFiles(rootFolder, "*.xml").Where(IsItemBagFile))
        {
            try
            {
                var doc = XDocument.Load(file);
                if (doc.Root is null || !doc.Root.Name.LocalName.Equals("ItemBag", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var bagConfig = doc.Root.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("BagConfig", StringComparison.OrdinalIgnoreCase));
                var bagRate = bagConfig is not null ? Math.Clamp(ReadIntAttr(bagConfig, "ItemRate", 10000), 1, 10000) : 10000;

                // Only "Monster_(N)_Name.xml" bags are what monster N drops. The others are what a box, an event, a
                // chaos mix or an NPC hands out (Item_, Event_, Mix_, NPC_, Buff_ ...): they were made drops of every
                // monster on every map, so a Lucky Coin reward bag of 100 % made every kill drop an item.
                var bagFileName = Path.GetFileName(file);
                var monsterBag = MonsterBagName.Match(bagFileName);
                var bagMonster = monsterBag.Success ? int.Parse(monsterBag.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
                var rewardBag = monsterBag.Success ? null : bagFileName;
                foreach (var allowEl in doc.Descendants().Where(e => e.Name.LocalName.Equals("DropAllow", StringComparison.OrdinalIgnoreCase)))
                {
                    var mapNum = ReadIntAttr(allowEl, "MapNumber", -1);
                    var drops = allowEl.Elements().Where(e => e.Name.LocalName.Equals("Drop", StringComparison.OrdinalIgnoreCase)).ToList();
                    var totalPoolRate = Math.Max(10000.0, drops.Sum(d => Math.Max(1, ReadIntAttr(d, "Rate", 10000))));
                    foreach (var dropEl in drops)
                    {
                        var poolRate = Math.Max(1, ReadIntAttr(dropEl, "Rate", 10000));
                        var items = dropEl.Elements().Where(e => e.Name.LocalName.Equals("Item", StringComparison.OrdinalIgnoreCase)).ToList();
                        if (items.Count == 0)
                        {
                            continue;
                        }

                        var effectiveChance = Math.Clamp((bagRate / 10000.0) * (poolRate / totalPoolRate) / items.Count, 0.0001, 1.0);
                        foreach (var itemEl in items)
                        {
                            if (TryCreateDropRule(itemEl, bagMonster, 0, 255, mapNum, effectiveChance, forceAncient: false, out var rule))
                            {
                                // The bag's ItemRate is the chance that the monster drops one item of it at all.
                                rule.GroupChance = bagRate / 10000.0;
                                rule.RewardBagName = rewardBag;
                                results.Add(rule);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed optional ItemBag XML file
            }
        }
    }

    private static bool IsItemBagFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        return dir is not null && Path.GetFileName(dir).Equals("ItemBags", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateDropRule(
        XElement item,
        int monsterNum,
        int minLv,
        int maxLv,
        int mapNum,
        double effectiveChance,
        bool forceAncient,
        out LegacyDropRule rule)
    {
        var cat = ReadIntAttr(item, "Cat", -1);
        var index = ReadIntAttr(item, "Index", -1);
        if (cat < 0 || cat > 15 || index < 0)
        {
            rule = null!;
            return false;
        }

        var minLevel = item.Attribute("ItemMinLevel") is not null
            ? ReadIntAttr(item, "ItemMinLevel", 0)
            : ReadIntAttr(item, "MinLevel", 0);

        rule = new LegacyDropRule
        {
            MonsterNumber = monsterNum,
            MonsterMinLevel = minLv,
            MonsterMaxLevel = maxLv,
            MapNumber = mapNum,
            ItemGroup = (byte)cat,
            ItemNumber = (short)index,
            Chance = Math.Round(effectiveChance, 6),
            MinItemLevel = (byte)Math.Clamp(minLevel, 0, 15),
            IsExcellent = ReadIntAttr(item, "Exc", 0) != 0,
            IsAncient = forceAncient || ReadIntAttr(item, "SetItem", 0) > 0,
            SocketCount = Math.Clamp(ReadIntAttr(item, "SocketCount", 0), 0, 5),
            ElementalAttribute = ReadIntAttr(item, "ElementalItem", 0),
            CompanionEvolutionGroup = int.TryParse(item.Attribute("MuunEvolutionItemCat")?.Value, out var evoCat) ? evoCat : null,
        };
        return true;
    }

    private static void AddStandardEventSchedule(
        string rootFolder,
        List<LegacyEventScheduleEntry> events,
        string xmlPattern,
        string iniPattern,
        string eventId,
        string name,
        string fallbackTimes,
        string rewardSummary,
        string? excludePattern = null)
    {
        var xmlFiles = FindFiles(rootFolder, xmlPattern)
            .Where(f => excludePattern is null || !Path.GetFileName(f).Contains(excludePattern, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var iniFiles = FindFiles(rootFolder, iniPattern)
            .Where(f => excludePattern is null || !Path.GetFileName(f).Contains(excludePattern, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (xmlFiles.Count == 0 && iniFiles.Count == 0)
        {
            return;
        }

        var parsedTimes = xmlFiles.Select(ReadScheduleTimesFromXml).FirstOrDefault(t => t is not null);
        events.Add(new LegacyEventScheduleEntry
        {
            EventId = eventId,
            Name = name,
            IsSeason9Only = false,
            ScheduleTimes = parsedTimes ?? fallbackTimes,
            RewardSummary = rewardSummary,
        });
    }

    private static string? ReadScheduleTimesFromXml(string filePath)
    {
        try
        {
            var doc = XDocument.Load(filePath);
            var starts = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("Start", StringComparison.OrdinalIgnoreCase) && e.Attribute("Hour") is not null)
                .Select(e =>
                {
                    var h = ReadIntAttr(e, "Hour", -1);
                    var m = e.Attribute("Minute") is not null ? ReadIntAttr(e, "Minute", 0) : ReadIntAttr(e, "Monute", 0);
                    return h >= 0 && h <= 23 && m >= 0 && m <= 59 ? $"{h:D2}:{m:D2}" : null;
                })
                .Where(s => s is not null)
                .Distinct(StringComparer.Ordinal)
                .Take(24)
                .Cast<string>()
                .ToList();
            return starts.Count > 0 ? string.Join(", ", starts) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void ParseInvasionSchedules(string rootFolder, List<LegacyEventScheduleEntry> events)
    {
        var foundXml = false;
        foreach (var file in FindFiles(rootFolder, "*InvasionManager*.xml"))
        {
            try
            {
                var doc = XDocument.Load(file);
                if (doc.Root is not null && ReadIntAttr(doc.Root, "Enable", 1) == 0)
                {
                    continue;
                }

                foreach (var inv in doc.Descendants().Where(e => e.Name.LocalName.Equals("Invasion", StringComparison.OrdinalIgnoreCase)))
                {
                    var typeId = ReadIntAttr(inv, "Type", -1);
                    var invName = inv.Attribute("Name")?.Value?.Trim() is { Length: > 0 } n ? n : $"Invasion {typeId}";
                    var times = inv.Elements()
                        .Where(e => e.Name.LocalName.Equals("Start", StringComparison.OrdinalIgnoreCase))
                        .Select(e => $"{Math.Clamp(ReadIntAttr(e, "Hour", 0), 0, 23):D2}:{Math.Clamp(ReadIntAttr(e, "Minute", 0), 0, 59):D2}")
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    if (times.Count > 0)
                    {
                        foundXml = true;
                        events.Add(new LegacyEventScheduleEntry
                        {
                            EventId = $"imported-invasion-{typeId}",
                            Name = invName,
                            IsSeason9Only = false,
                            ScheduleTimes = string.Join(", ", times),
                            RewardSummary = "Scheduled field invasion",
                        });
                    }
                }
            }
            catch
            {
                // Ignore malformed InvasionManager XML
            }
        }

        if (foundXml)
        {
            return;
        }

        foreach (var file in FindFiles(rootFolder, "EventsManager.dat"))
        {
            var inSection = false;
            var grouped = new Dictionary<int, List<string>>();
            foreach (var rawLine in File.ReadLines(file))
            {
                var line = StripLineComment(rawLine).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.Equals("end", StringComparison.OrdinalIgnoreCase))
                {
                    inSection = false;
                    continue;
                }

                var tokens = line.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 1 && TryParseInt(tokens[0], out var sec) && sec == 0)
                {
                    inSection = true;
                    continue;
                }

                if (inSection && tokens.Length >= 3
                    && TryParseInt(tokens[0], out var typeId)
                    && TryParseInt(tokens[1], out var h)
                    && TryParseInt(tokens[2], out var m))
                {
                    if (!grouped.TryGetValue(typeId, out var list))
                    {
                        list = [];
                        grouped[typeId] = list;
                    }

                    var ts = $"{Math.Clamp(h, 0, 23):D2}:{Math.Clamp(m, 0, 59):D2}";
                    if (!list.Contains(ts))
                    {
                        list.Add(ts);
                    }
                }
            }

            foreach (var (typeId, times) in grouped.OrderBy(kv => kv.Key))
            {
                events.Add(new LegacyEventScheduleEntry
                {
                    EventId = $"imported-invasion-{typeId}",
                    Name = $"Invasion #{typeId}",
                    IsSeason9Only = false,
                    ScheduleTimes = string.Join(", ", times),
                    RewardSummary = "Scheduled field invasion (EventsManager.dat)",
                });
            }
        }
    }

    private static void ParseXmlMonsterSpawns(string rootFolder, List<LegacySpawnEntry> spawns)
    {
        foreach (var file in FindFiles(rootFolder, "*MonsterSpawn*.xml"))
        {
            try
            {
                var doc = XDocument.Load(file);
                foreach (var mapEl in doc.Descendants().Where(e => e.Name.LocalName.Equals("Map", StringComparison.OrdinalIgnoreCase)))
                {
                    var mapId = ReadIntAttr(mapEl, "Number", -1);
                    if (mapId < 0)
                    {
                        continue;
                    }

                    foreach (var spotEl in mapEl.Elements().Where(e => e.Name.LocalName.Equals("Spot", StringComparison.OrdinalIgnoreCase)))
                    {
                        var spotType = ReadIntAttr(spotEl, "Type", 2);
                        foreach (var spawnEl in spotEl.Elements().Where(e => e.Name.LocalName.Equals("Spawn", StringComparison.OrdinalIgnoreCase)))
                        {
                            var monsterId = ReadIntAttr(spawnEl, "Index", -1);
                            var x1 = ReadIntAttr(spawnEl, "StartX", -1);
                            var y1 = ReadIntAttr(spawnEl, "StartY", -1);
                            if (monsterId < 0 || x1 < 0 || y1 < 0)
                            {
                                continue;
                            }

                            // Distance (30 for nearly every monster, 0 for NPCs, as the third column of MonsterSetBase.txt)
                            // is how far the monster roams, not a spawn area: a single spot stays a single cell.
                            var x2 = spawnEl.Attribute("EndX") is not null ? ReadIntAttr(spawnEl, "EndX", x1) : x1;
                            var y2 = spawnEl.Attribute("EndY") is not null ? ReadIntAttr(spawnEl, "EndY", y1) : y1;
                            var dir = ReadIntAttr(spawnEl, "Dir", 0);
                            var count = Math.Max(1, ReadIntAttr(spawnEl, "Count", 1));
                            var element = ReadIntAttr(spawnEl, "Element", 0);

                            // Elemental spots (Type 3 / Element > 0) map to group 5 until Wave 4B adds elemental monster mechanics.
                            var mappedGroup = spotType == 3 || element > 0 ? 5 : spotType;
                            spawns.Add(new LegacySpawnEntry
                            {
                                SpawnGroup = mappedGroup,
                                MonsterNumber = monsterId,
                                MapNumber = mapId,
                                X1 = (byte)Math.Clamp(Math.Min(x1, x2), 0, 255),
                                Y1 = (byte)Math.Clamp(Math.Min(y1, y2), 0, 255),
                                X2 = (byte)Math.Clamp(Math.Max(x1, x2), 0, 255),
                                Y2 = (byte)Math.Clamp(Math.Max(y1, y2), 0, 255),
                                Direction = dir,
                                Quantity = count,
                            });
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed optional XML spawn file
            }
        }
    }

    /// <summary>
    /// Finds the server's files of a pattern, leaving out the suite's "Tools" folders: their editors carry copies and
    /// samples of the data files (Tools/ToolKit Silver/Data/MSB/MonsterSetBase.txt), which were imported as well.
    /// </summary>
    private static IEnumerable<string> FindFiles(string rootFolder, string pattern) =>
        Directory.EnumerateFiles(rootFolder, pattern, SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(rootFolder, p)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("Tools", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

    private static int? ReadPercent(Dictionary<string, string> keyValues, string key)
        => keyValues.TryGetValue(key, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? Math.Clamp(value, 0, 100)
            : null;

    private static bool IsCastleSiegeFile(string filePath)
        => Path.GetFileNameWithoutExtension(filePath).EndsWith("_CS", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ReadIniKeyValues(string filePath)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(filePath))
        {
            var cleaned = StripIniComment(rawLine).Trim();
            var eq = cleaned.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = cleaned[..eq].Trim();
            var val = cleaned[(eq + 1)..].Trim().Trim('"');
            if (!string.IsNullOrEmpty(key))
            {
                map[key] = val;
            }
        }

        return map;
    }

    private static string StripIniComment(string line)
    {
        var semi = line.IndexOf(';');
        var slash = line.IndexOf("//", StringComparison.Ordinal);
        var idx = semi >= 0 && slash >= 0 ? Math.Min(semi, slash) : Math.Max(semi, slash);
        return idx >= 0 ? line[..idx] : line;
    }

    private static string StripLineComment(string line)
    {
        var slash = line.IndexOf("//", StringComparison.Ordinal);
        return slash >= 0 ? line[..slash] : line;
    }

    private static string? ResolveShopFile(string rootFolder, string shopListDir, string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(shopListDir, fileName),
            Path.Combine(shopListDir, "Shops", fileName),
            Path.Combine(rootFolder, "Shops", fileName),
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory.EnumerateFiles(rootFolder, Path.GetFileName(fileName), SearchOption.AllDirectories).FirstOrDefault();
    }

    private static List<LegacyShopItemEntry> ParseShopXml(string filePath)
    {
        var items = new List<LegacyShopItemEntry>();
        try
        {
            var doc = XDocument.Load(filePath);
            foreach (var el in doc.Descendants().Where(e => e.Name.LocalName.Equals("Item", StringComparison.OrdinalIgnoreCase)))
            {
                var cat = ReadIntAttr(el, "Cat", -1);
                var idx = ReadIntAttr(el, "Index", -1);
                if (cat < 0 || cat > 15 || idx < 0)
                {
                    continue;
                }

                items.Add(new LegacyShopItemEntry
                {
                    Group = (byte)cat,
                    Number = (short)idx,
                    Level = (byte)Math.Clamp(ReadIntAttr(el, "Level", 0), 0, 15),
                    Durability = (byte)Math.Clamp(ReadIntAttr(el, "Durability", 255), 0, 255),
                    Skill = ReadIntAttr(el, "Skill", 0) > 0,
                    Luck = ReadIntAttr(el, "Luck", 0) > 0,
                    OptionLevel = (byte)Math.Clamp(ReadIntAttr(el, "Option", 0), 0, 7),
                    ExcellentBits = (byte)Math.Clamp(ReadIntAttr(el, "Exc", 0), 0, 63),
                    ElementalAttribute = ReadIntAttr(el, "Elemental", 0),
                });
            }
        }
        catch
        {
            // Skip malformed shop file
        }

        return items;
    }

    private static List<LegacyShopItemEntry> ParseShopTxt(string filePath)
    {
        var items = new List<LegacyShopItemEntry>();
        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = StripLineComment(rawLine).Trim();
            if (line.Length == 0 || line.Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tokens = line.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 7
                && TryParseInt(tokens[0], out var cat)
                && TryParseInt(tokens[1], out var idx)
                && cat >= 0 && cat <= 15 && idx >= 0)
            {
                TryParseInt(tokens[2], out var level);
                TryParseInt(tokens[3], out var dur);
                TryParseInt(tokens[4], out var skill);
                TryParseInt(tokens[5], out var luck);
                TryParseInt(tokens[6], out var opt);
                var exc = tokens.Length >= 8 && TryParseInt(tokens[7], out var excVal) ? excVal : 0;

                items.Add(new LegacyShopItemEntry
                {
                    Group = (byte)cat,
                    Number = (short)idx,
                    Level = (byte)Math.Clamp(level, 0, 15),
                    Durability = (byte)Math.Clamp(dur, 0, 255),
                    Skill = skill > 0,
                    Luck = luck > 0,
                    OptionLevel = (byte)Math.Clamp(opt, 0, 7),
                    ExcellentBits = (byte)Math.Clamp(exc, 0, 63),
                });
            }
        }

        return items;
    }

    private static int ReadIntAttr(XElement el, string name, int fallback)
    {
        var val = el.Attribute(name)?.Value;
        return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static bool TryReadFloatAttr(XElement el, string name, out float value)
    {
        var raw = el.Attribute(name)?.Value;
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseInt(string raw, out int value) =>
        int.TryParse(Regex.Replace(raw, @"\D+$", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}
