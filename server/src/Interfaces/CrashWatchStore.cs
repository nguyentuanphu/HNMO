// <copyright file="CrashWatchStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Persisted file store for unexpected server crash incidents under <c>save/crash-incidents.json</c>.
/// </summary>
public static class CrashWatchStore
{
    private const int MaxStoredIncidents = 50;
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Records a new crash incident and returns the updated list.</summary>
    /// <param name="saveDirectory">Save folder path.</param>
    /// <param name="incident">Crash incident to append.</param>
    /// <returns>Updated list newest first.</returns>
    public static IReadOnlyList<CrashIncident> RecordIncident(string saveDirectory, CrashIncident incident)
    {
        lock (Gate)
        {
            var list = ReadLocked(saveDirectory);
            list.Insert(0, incident);
            if (list.Count > MaxStoredIncidents)
            {
                list.RemoveRange(MaxStoredIncidents, list.Count - MaxStoredIncidents);
            }

            WriteLocked(saveDirectory, list);
            return list;
        }
    }

    /// <summary>Returns recent crash incidents (newest first).</summary>
    /// <param name="saveDirectory">Save folder path.</param>
    /// <returns>Crash incident list.</returns>
    public static IReadOnlyList<CrashIncident> ListIncidents(string saveDirectory)
    {
        lock (Gate)
        {
            return ReadLocked(saveDirectory);
        }
    }

    /// <summary>Marks all stored crash incidents as acknowledged by the admin.</summary>
    /// <param name="saveDirectory">Save folder path.</param>
    /// <returns>Number of incidents newly acknowledged.</returns>
    public static int AcknowledgeAll(string saveDirectory)
    {
        lock (Gate)
        {
            var list = ReadLocked(saveDirectory);
            var changed = 0;
            foreach (var item in list)
            {
                if (!item.Acknowledged)
                {
                    item.Acknowledged = true;
                    changed++;
                }
            }

            if (changed > 0)
            {
                WriteLocked(saveDirectory, list);
            }

            return changed;
        }
    }

    private static string FilePath(string saveDirectory) => Path.Combine(saveDirectory, "crash-incidents.json");

    private static List<CrashIncident> ReadLocked(string saveDirectory)
    {
        var path = FilePath(saveDirectory);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<CrashIncident>>(File.ReadAllText(path));
            return items?.OrderByDescending(i => i.OccurredAtUtc).ToList() ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private static void WriteLocked(string saveDirectory, List<CrashIncident> list)
    {
        Directory.CreateDirectory(saveDirectory);
        var path = FilePath(saveDirectory);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(list, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }
}
