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

        if (hasBattleCore || hasMuun || hasPentagram || hasArca || hasTormented
            || signals.Any(s => s.Contains(">200", StringComparison.Ordinal)))
        {
            return (LegacyServerVersion.Season9, signals);
        }

        var hasS6Data = FindFiles(rootFolder, "*Common.ini").Any()
            || FindFiles(rootFolder, "CommonServer.cfg").Any()
            || FindFiles(rootFolder, "*ShopList*.xml").Any()
            || FindFiles(rootFolder, "*MonsterSetBase*.txt").Any();
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

            if (kv.TryGetValue("ZenDropMultipler", out var zenStr) || kv.TryGetValue("ZenDropMultiplier", out zenStr))
            {
                if (double.TryParse(zenStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var zenMult))
                {
                    rates.ZenMultiplier = Math.Max(0.0, zenMult);
                }
            }
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
                        var cat = ReadIntAttr(item, "Cat", -1);
                        var index = ReadIntAttr(item, "Index", -1);
                        if (cat < 0 || cat > 15 || index < 0)
                        {
                            continue;
                        }

                        var weight = Math.Max(1, ReadIntAttr(item, "DropRate", 1));
                        var effectiveChance = Math.Clamp((sectionRate / 10000.0) * (weight / totalWeight), 0.0001, 1.0);
                        results.Add(new LegacyDropRule
                        {
                            MonsterNumber = monsterNum,
                            MonsterMinLevel = minLv,
                            MonsterMaxLevel = maxLv,
                            MapNumber = mapNum,
                            ItemGroup = (byte)cat,
                            ItemNumber = (short)index,
                            Chance = Math.Round(effectiveChance, 6),
                            MinItemLevel = (byte)Math.Clamp(ReadIntAttr(item, "ItemMinLevel", 0), 0, 15),
                            IsExcellent = ReadIntAttr(item, "Exc", 0) > 0,
                            IsAncient = ReadIntAttr(item, "SetItem", 0) > 0,
                            SocketCount = Math.Clamp(ReadIntAttr(item, "SocketCount", 0), 0, 5),
                            ElementalAttribute = ReadIntAttr(item, "ElementalItem", 0),
                            CompanionEvolutionGroup = int.TryParse(item.Attribute("MuunEvolutionItemCat")?.Value, out var evoCat) ? evoCat : null,
                        });
                    }
                }
            }
            catch
            {
                // Ignore malformed optional XML file
            }
        }

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
                    if (npcIndex < 0 || string.IsNullOrWhiteSpace(fileName))
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
                    var mapNum = ReadIntAttr(ev, "MapNumber", -1);
                    events.Add(new LegacyEventScheduleEntry
                    {
                        EventId = $"imported-bonus-{idx}",
                        Name = ev.Attribute("Name")?.Value?.Trim() is { Length: > 0 } n ? n : $"Bonus Event {idx}",
                        IsSeason9Only = false,
                        MapNumber = mapNum >= 0 ? (short)mapNum : null,
                        ScheduleTimes = $"{startH:D2}:{startM:D2}",
                        ExpBonusPercent = ReadIntAttr(ev, "ExpBonus", 20),
                        DropBonusPercent = ReadIntAttr(ev, "DropBonus", 15),
                    });
                }
            }
            catch
            {
                // Ignore malformed XML
            }
        }

        if (FindFiles(rootFolder, "*BloodCastle*.xml").Concat(FindFiles(rootFolder, "*BloodCastle*.ini")).Any())
        {
            events.Add(new LegacyEventScheduleEntry
            {
                EventId = "imported-blood-castle",
                Name = "Blood Castle",
                IsSeason9Only = false,
                ScheduleTimes = "00:30, 02:30, 04:30, 06:30, 08:30, 10:30, 12:30, 14:30, 16:30, 18:30, 20:30, 22:30",
                RewardSummary = "Jewel of Chaos / Jewel of Bless",
            });
        }

        if (FindFiles(rootFolder, "*DevilSquare*.xml").Concat(FindFiles(rootFolder, "*DevilSquare*.ini")).Any())
        {
            events.Add(new LegacyEventScheduleEntry
            {
                EventId = "imported-devil-square",
                Name = "Devil Square",
                IsSeason9Only = false,
                ScheduleTimes = "01:00, 05:00, 09:00, 13:00, 17:00, 21:00",
                RewardSummary = "Bonus EXP & Zen ranking",
            });
        }

        if (FindFiles(rootFolder, "*ChaosCastle*.xml").Concat(FindFiles(rootFolder, "*ChaosCastle*.ini")).Any())
        {
            events.Add(new LegacyEventScheduleEntry
            {
                EventId = "imported-chaos-castle",
                Name = "Chaos Castle",
                IsSeason9Only = false,
                ScheduleTimes = "01:30, 05:30, 09:30, 13:30, 17:30, 21:30",
                RewardSummary = "Jewels / Ancient chance",
            });
        }

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
            if (FindFiles(rootFolder, pattern).Any())
            {
                events.Add(new LegacyEventScheduleEntry
                {
                    EventId = $"s9-{label.ToLowerInvariant().Replace(' ', '-')}",
                    Name = label,
                    IsSeason9Only = true,
                    ScheduleTimes = "20:00",
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

        foreach (var file in FindFiles(rootFolder, "MonsterSetBase*.txt"))
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

        return spawns;
    }

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

    private static IEnumerable<string> FindFiles(string rootFolder, string pattern) =>
        Directory.EnumerateFiles(rootFolder, pattern, SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

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
