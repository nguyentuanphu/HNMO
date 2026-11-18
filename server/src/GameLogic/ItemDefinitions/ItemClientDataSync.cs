// <copyright file="ItemClientDataSync.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.ItemDefinitions;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HNMO.Interfaces;

/// <summary>
/// Synchronizes item definitions from the admin panel to the server configuration and web client <c>Data/Items</c> and <c>Data/Effects/GlowColors.json</c> files.
/// </summary>
public static class ItemClientDataSync
{
    /// <summary>
    /// File stems for item groups 0-15 under <c>Data/Items</c> and <c>Data/Items/Models</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> GroupFileNames =
    [
        "Group00_Sword",
        "Group01_Axe",
        "Group02_Mace",
        "Group03_Spear",
        "Group04_Bow",
        "Group05_Staff",
        "Group06_Shield",
        "Group07_Helm",
        "Group08_Armor",
        "Group09_Pants",
        "Group10_Gloves",
        "Group11_Boots",
        "Group12_Wing",
        "Group13_Helper",
        "Group14_Potion",
        "Group15_Etc",
    ];

    /// <summary>
    /// Built-in named glow colors defined in <c>Data/Effects/GlowColors.json</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> StandardGlowColors =
    [
        "orange", "red", "blue", "green", "white", "lime", "lavender", "paleBlue", "steelBlue",
        "sand", "slate", "sage", "navy", "stone", "denim", "copper", "bronze", "brown",
        "royalBlue", "leafGreen", "violet", "olive", "dusk", "ochre", "grey", "mist",
        "emerald", "rust", "amber", "purple", "pink", "lilac", "rose", "periwinkle",
        "orchid", "cornflower", "moss", "crimson", "magenta", "darkOrange", "goldenrod",
        "wheat", "ice", "gold", "azure",
    ];

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Normalizes a glow color name or <c>#rrggbb</c> hex string.
    /// </summary>
    /// <param name="glowColor">The input color string.</param>
    /// <returns>The normalized value, or empty string when invalid/unset.</returns>
    public static string NormalizeGlowColor(string? glowColor)
    {
        if (string.IsNullOrWhiteSpace(glowColor))
        {
            return string.Empty;
        }

        var trimmed = glowColor.Trim();
        var preset = StandardGlowColors.FirstOrDefault(c => c.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (preset is not null)
        {
            return preset;
        }

        if (IsHexColor(trimmed))
        {
            return trimmed.ToLowerInvariant();
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks whether a string is a 6-digit hex color (<c>#rrggbb</c>).
    /// </summary>
    /// <param name="value">The candidate string.</param>
    /// <returns><see langword="true"/> if valid <c>#rrggbb</c>.</returns>
    public static bool IsHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < 7; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Attempts to locate the loose <c>Data</c> folder on disk containing <c>Items/Group00_Sword.json</c>.
    /// </summary>
    /// <param name="configuredDataPath">Optional configured path (relative or absolute).</param>
    /// <returns>The full directory path if loose item JSON files exist; otherwise <see langword="null"/>.</returns>
    public static string? TryResolveDataRoot(string? configuredDataPath = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredDataPath))
        {
            candidates.Add(Path.GetFullPath(configuredDataPath));
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && current is not null; depth++)
        {
            candidates.Add(Path.Combine(current.FullName, "Data"));
            candidates.Add(Path.Combine(current.FullName, "client", "src", "bin", "Data"));
            current = current.Parent;
        }

        var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var depth = 0; depth < 6 && cwd is not null; depth++)
        {
            candidates.Add(Path.Combine(cwd.FullName, "Data"));
            candidates.Add(Path.Combine(cwd.FullName, "client", "src", "bin", "Data"));
            cwd = cwd.Parent;
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(Path.Combine(candidate, "Items", "Group00_Sword.json")))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Computes updated JSON payloads for all affected <c>Data/Items</c>, <c>Data/Items/Models</c>, and <c>Data/Effects/GlowColors.json</c> files.
    /// </summary>
    /// <param name="dataRoot">The loose <c>Data</c> directory.</param>
    /// <param name="overrides">The overrides to apply.</param>
    /// <returns>A dictionary mapping relative paths (using <c>/</c>) to updated JSON strings.</returns>
    public static IReadOnlyDictionary<string, string> BuildPatchedFiles(string dataRoot, IReadOnlyList<ItemClientMetadata> overrides)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(overrides);

        var updated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (overrides.Count == 0)
        {
            return updated;
        }

        var customColorsNeeded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var meta in overrides)
        {
            var normalized = NormalizeGlowColor(meta.GlowColor);
            if (IsHexColor(normalized))
            {
                var key = ToGlowColorKey(normalized);
                customColorsNeeded[normalized] = key;
            }
        }

        if (customColorsNeeded.Count > 0)
        {
            var glowRel = "Effects/GlowColors.json";
            var glowFull = Path.Combine(dataRoot, "Effects", "GlowColors.json");
            if (File.Exists(glowFull))
            {
                var root = JsonNode.Parse(File.ReadAllText(glowFull))?.AsObject() ?? new JsonObject();
                var colorsObj = root["colors"]?.AsObject() ?? new JsonObject();
                root["colors"] = colorsObj;
                foreach (var (hex, key) in customColorsNeeded)
                {
                    if (TryParseHexRgb(hex, out var rgb))
                    {
                        colorsObj[key] = new JsonArray(rgb[0], rgb[1], rgb[2]);
                    }
                }

                updated[glowRel] = root.ToJsonString(WriteOptions) + Environment.NewLine;
            }
        }

        var modelFilesCache = new Dictionary<int, JsonObject>();
        foreach (var byGroup in overrides.GroupBy(o => (int)o.Group))
        {
            if (byGroup.Key < 0 || byGroup.Key >= GroupFileNames.Count)
            {
                continue;
            }

            var fileName = $"{GroupFileNames[byGroup.Key]}.json";
            var itemRel = $"Items/{fileName}";
            var itemFull = Path.Combine(dataRoot, "Items", fileName);
            if (File.Exists(itemFull))
            {
                var root = JsonNode.Parse(File.ReadAllText(itemFull))?.AsObject() ?? new JsonObject();
                var itemsArray = root["items"]?.AsArray() ?? new JsonArray();
                root["items"] = itemsArray;

                foreach (var meta in byGroup)
                {
                    PatchItemJson(itemsArray, meta);
                }

                updated[itemRel] = root.ToJsonString(WriteOptions) + Environment.NewLine;
            }

            var modelRel = $"Items/Models/{fileName}";
            var modelFull = Path.Combine(dataRoot, "Items", "Models", fileName);
            if (File.Exists(modelFull))
            {
                var modelRoot = GetOrLoadModelRoot(dataRoot, modelFilesCache, byGroup.Key);
                if (modelRoot is not null)
                {
                    var changed = false;
                    foreach (var meta in byGroup)
                    {
                        var normalizedGlow = NormalizeGlowColor(meta.GlowColor);
                        var resolvedColorName = IsHexColor(normalizedGlow) ? ToGlowColorKey(normalizedGlow) : normalizedGlow;
                        if (PatchModelJson(dataRoot, modelFilesCache, modelRoot, meta, resolvedColorName))
                        {
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        updated[modelRel] = modelRoot.ToJsonString(WriteOptions) + Environment.NewLine;
                    }
                }
            }
        }

        return updated;
    }

    /// <summary>
    /// Writes updated item JSON files into the specified loose <c>Data</c> folder.
    /// </summary>
    /// <param name="dataRoot">The target loose <c>Data</c> directory.</param>
    /// <param name="overrides">The metadata overrides to write.</param>
    /// <returns>The list of modified relative file paths.</returns>
    public static IReadOnlyList<string> ApplyOverridesToDataDirectory(string dataRoot, IReadOnlyList<ItemClientMetadata> overrides)
    {
        var patched = BuildPatchedFiles(dataRoot, overrides);
        var written = new List<string>(patched.Count);
        foreach (var (rel, json) in patched)
        {
            var fullPath = Path.Combine(dataRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            written.Add(rel);
        }

        return written;
    }

    /// <summary>
    /// Maps a <c>#rrggbb</c> hex color into a canonical <c>GlowColors.json</c> key.
    /// </summary>
    /// <param name="hexColor">The normalized <c>#rrggbb</c> hex string.</param>
    /// <returns>The <c>GlowColors.json</c> entry key.</returns>
    public static string ToGlowColorKey(string hexColor) => $"custom_{hexColor.TrimStart('#').ToLowerInvariant()}";

    private static bool TryParseHexRgb(string hex, out double[] rgb)
    {
        rgb = [1, 1, 1];
        if (!IsHexColor(hex))
        {
            return false;
        }

        var r = int.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        rgb = [Math.Round(r / 255.0, 3), Math.Round(g / 255.0, 3), Math.Round(b / 255.0, 3)];
        return true;
    }

    private static void PatchItemJson(JsonArray itemsArray, ItemClientMetadata meta)
    {
        JsonObject? entry = null;
        foreach (var node in itemsArray)
        {
            if (node is JsonObject obj && obj["number"]?.GetValue<int>() == meta.Number)
            {
                entry = obj;
                break;
            }
        }

        if (entry is null)
        {
            entry = new JsonObject
            {
                ["number"] = meta.Number,
            };
            itemsArray.Add(entry);
        }

        var nameObj = entry["name"]?.AsObject() ?? new JsonObject();
        entry["name"] = nameObj;
        nameObj["en"] = meta.NameEn;
        if (!string.IsNullOrWhiteSpace(meta.NameVi))
        {
            nameObj["vi"] = meta.NameVi;
        }
        else
        {
            nameObj.Remove("vi");
        }

        entry["width"] = meta.Width;
        entry["height"] = meta.Height;
        entry["level"] = meta.DropLevel;
        entry["maximumItemLevel"] = meta.MaximumItemLevel;
        entry["durability"] = meta.Durability;
        if (meta.BuyPrice > 0)
        {
            entry["buyPrice"] = meta.BuyPrice;
        }

        var reqObj = entry["requirements"]?.AsObject() ?? new JsonObject();
        SetOptionalPositive(reqObj, "level", meta.LevelRequirement);
        SetOptionalPositive(reqObj, "strength", meta.StrengthRequirement);
        SetOptionalPositive(reqObj, "dexterity", meta.AgilityRequirement);
        SetOptionalPositive(reqObj, "vitality", meta.VitalityRequirement);
        SetOptionalPositive(reqObj, "energy", meta.EnergyRequirement);
        SetOptionalPositive(reqObj, "leadership", meta.LeadershipRequirement);
        if (reqObj.Count > 0)
        {
            entry["requirements"] = reqObj;
        }
        else
        {
            entry.Remove("requirements");
        }

        if (!string.IsNullOrWhiteSpace(meta.TooltipTextEn) || !string.IsNullOrWhiteSpace(meta.TooltipTextVi))
        {
            var tooltipObj = new JsonObject();
            if (!string.IsNullOrWhiteSpace(meta.TooltipTextEn))
            {
                tooltipObj["en"] = meta.TooltipTextEn.Trim();
            }

            if (!string.IsNullOrWhiteSpace(meta.TooltipTextVi))
            {
                tooltipObj["vi"] = meta.TooltipTextVi.Trim();
            }

            entry["tooltip"] = tooltipObj;
        }
        else
        {
            entry.Remove("tooltip");
        }
    }

    private static bool PatchModelJson(
        string dataRoot,
        Dictionary<int, JsonObject> cache,
        JsonObject modelRoot,
        ItemClientMetadata meta,
        string resolvedColorName)
    {
        var models = modelRoot["models"]?.AsArray() ?? new JsonArray();
        modelRoot["models"] = models;

        JsonObject? entry = null;
        foreach (var node in models)
        {
            if (node is JsonObject obj && obj["number"]?.GetValue<int>() == meta.Number)
            {
                entry = obj;
                break;
            }
        }

        var changed = false;
        if (entry is null && meta.ModelSourceGroup is { } srcGroup && meta.ModelSourceNumber is { } srcNumber)
        {
            var srcRoot = GetOrLoadModelRoot(dataRoot, cache, srcGroup);
            var srcModels = srcRoot?["models"]?.AsArray();
            var srcEntry = srcModels?
                .OfType<JsonObject>()
                .FirstOrDefault(o => o["number"]?.GetValue<int>() == srcNumber);
            if (srcEntry is not null)
            {
                entry = srcEntry.DeepClone().AsObject();
                entry["number"] = meta.Number;
                models.Add(entry);
                changed = true;
            }
        }

        if (entry is not null && !string.IsNullOrWhiteSpace(resolvedColorName))
        {
            var glowObj = entry["glow"]?.AsObject() ?? new JsonObject();
            entry["glow"] = glowObj;
            glowObj["color"] = resolvedColorName;
            changed = true;
        }

        return changed;
    }

    private static JsonObject? GetOrLoadModelRoot(string dataRoot, Dictionary<int, JsonObject> cache, int group)
    {
        if (group < 0 || group >= GroupFileNames.Count)
        {
            return null;
        }

        if (cache.TryGetValue(group, out var cached))
        {
            return cached;
        }

        var path = Path.Combine(dataRoot, "Items", "Models", $"{GroupFileNames[group]}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var parsed = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        if (parsed is not null)
        {
            cache[group] = parsed;
        }

        return parsed;
    }

    private static void SetOptionalPositive(JsonObject target, string key, int value)
    {
        if (value > 0)
        {
            target[key] = value;
        }
        else
        {
            target.Remove(key);
        }
    }
}
