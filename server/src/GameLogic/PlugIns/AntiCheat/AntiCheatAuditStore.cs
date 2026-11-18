// <copyright file="AntiCheatAuditStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AntiCheat;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> for recent anti-cheat audit records.
/// </summary>
public sealed class AntiCheatAuditStore
{
    private const string SectionKey = "antiCheatAudit";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly HnmoStateStore _store;

    private AntiCheatAuditStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the audit store backed by <paramref name="filePath"/> (or a temporary file in tests).</summary>
    /// <param name="filePath">State file path.</param>
    /// <returns>The store instance.</returns>
    public static AntiCheatAuditStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-anticheat-{Guid.NewGuid():N}.json"));
        return new AntiCheatAuditStore(store);
    }

    /// <summary>Returns recent audit entries newest-first.</summary>
    /// <returns>List of audit entries.</returns>
    public List<AntiCheatViolationEntry> ListRecent()
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<List<AntiCheatViolationEntry>>(SectionKey, JsonOptions) ?? [];
            return data.OrderByDescending(x => x.OccurredAtUtc).ToList();
        }
    }

    /// <summary>Appends a new violation entry and trims to <paramref name="maxEntries"/>.</summary>
    /// <param name="entry">Entry to append.</param>
    /// <param name="maxEntries">Maximum entries retained.</param>
    public void Append(AntiCheatViolationEntry entry, int maxEntries)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (this._gate)
        {
            var list = this._store.ReadSection<List<AntiCheatViolationEntry>>(SectionKey, JsonOptions) ?? [];
            if (string.IsNullOrWhiteSpace(entry.OccurredAtSydney))
            {
                var sydney = HnmoStateStore.ToTimeZone(entry.OccurredAtUtc, HnmoStateStore.ResolveTimeZone(null));
                entry.OccurredAtSydney = sydney.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }

            list.Insert(0, entry);
            var cap = Math.Clamp(maxEntries, 10, 5000);
            if (list.Count > cap)
            {
                list.RemoveRange(cap, list.Count - cap);
            }

            this._store.WriteSection(SectionKey, list, JsonOptions);
        }
    }

    /// <summary>Clears all stored violation audit entries.</summary>
    public void Clear()
    {
        lock (this._gate)
        {
            this._store.WriteSection(SectionKey, new List<AntiCheatViolationEntry>(), JsonOptions);
        }
    }
}
