// <copyright file="QolStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Qol;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> (<c>qol</c>) for mining audit entries.
/// </summary>
public sealed class QolStore
{
    private const string SectionKey = "qol";
    private const int MaxLogEntries = 50;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly HnmoStateStore _stateStore;

    private QolStore(HnmoStateStore stateStore)
    {
        this._stateStore = stateStore;
    }

    /// <summary>Opens the QoL store backed by <paramref name="filePath"/>.</summary>
    /// <param name="filePath">Target state file path.</param>
    /// <returns>Store instance.</returns>
    public static QolStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-qol-{Guid.NewGuid():N}.json"));
        return new(store);
    }

    /// <summary>Reads current persisted state snapshot.</summary>
    /// <returns>State snapshot.</returns>
    public QolState Snapshot()
    {
        lock (this._gate)
        {
            return this._stateStore.ReadSection<QolState>(SectionKey, JsonOptions) ?? new QolState();
        }
    }

    /// <summary>Appends a Sydney-timestamped mining find entry.</summary>
    /// <param name="characterName">Miner character name.</param>
    /// <param name="rewardName">Reward description.</param>
    /// <param name="utcNow">UTC timestamp.</param>
    public void RecordMine(string characterName, string rewardName, DateTime utcNow)
    {
        var sydney = HnmoStateStore.ToTimeZone(utcNow, HnmoStateStore.ResolveTimeZone(null));
        var line = $"[{sydney.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}] {characterName}: {rewardName}";
        lock (this._gate)
        {
            var state = this._stateStore.ReadSection<QolState>(SectionKey, JsonOptions) ?? new QolState();
            state.TotalMinesCompleted++;
            state.RecentMineLogs.Insert(0, line);
            if (state.RecentMineLogs.Count > MaxLogEntries)
            {
                state.RecentMineLogs = state.RecentMineLogs.Take(MaxLogEntries).ToList();
            }

            this._stateStore.WriteSection(SectionKey, state, JsonOptions);
        }
    }
}
