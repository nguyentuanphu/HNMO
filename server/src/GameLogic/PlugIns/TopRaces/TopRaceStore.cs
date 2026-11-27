// <copyright file="TopRaceStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// Keeps the <see cref="TopRaceStoreData"/>; with a file, it outlasts a restart of the server. All game servers, the
/// admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class TopRaceStore
{
    private const string FeatureRuns = "topRaces.run";
    private const string FeatureSeen = "topRaces.seen";
    private const string FeatureOwed = "topRaces.owed";
    private const string SectionKey = "topRaces";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, TopRaceStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<TopRaceRun> _runsSync = new();
    private readonly HnmoFeatureRowSync<TopRaceSeen> _seenSync = new();
    private readonly HnmoFeatureRowSync<List<TopRaceOwedReward>> _owedSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopRaceStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public TopRaceStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public TopRaceStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static TopRaceStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new TopRaceStore(p.Length == 0 ? null : p));
    }

    /// <summary>Marks the data as changed.</summary>
    public void Changed() => this._changed = true;

    /// <summary>
    /// Saves the data when it changed. The caller holds <see cref="Lock"/>.
    /// </summary>
    public void SaveIfChanged()
    {
        if (!this._changed || this._stateStore is null)
        {
            this._changed = false;
            return;
        }

        static DateTime? RunExpiry(string runId, TopRaceRun run) =>
            run.EndUtc == DateTime.MinValue || run.EndUtc == DateTime.MaxValue ? null : run.EndUtc;

        this._runsSync.SaveChanges(this._rowBackend, FeatureRuns, this.Data.Runs, JsonOptions, RunExpiry, useDirtySetOnly: true);
        this._seenSync.SaveChanges(this._rowBackend, FeatureSeen, this.Data.Seen, JsonOptions, useDirtySetOnly: true);
        this._owedSync.SaveChanges(this._rowBackend, FeatureOwed, this.Data.Owed, JsonOptions, useDirtySetOnly: true);

        try
        {
            this._stateStore.WriteSection(SectionKey, new TopRaceStoreData { History = this.Data.History }, JsonOptions);
            this._changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private TopRaceStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new TopRaceStoreData();
        }

        var data = this._stateStore.ReadSection<TopRaceStoreData>(SectionKey, JsonOptions) ?? new TopRaceStoreData();
        data.Runs = new Dictionary<string, TopRaceRun>(data.Runs, StringComparer.Ordinal);
        data.Seen = new Dictionary<string, TopRaceSeen>(data.Seen, StringComparer.Ordinal);
        data.Owed = new Dictionary<string, List<TopRaceOwedReward>>(data.Owed, StringComparer.Ordinal);

        static DateTime? RunExpiry(string runId, TopRaceRun run) =>
            run.EndUtc == DateTime.MinValue || run.EndUtc == DateTime.MaxValue ? null : run.EndUtc;
        var m1 = this._runsSync.LoadOrMigrate(this._rowBackend, FeatureRuns, data.Runs, JsonOptions, RunExpiry);
        var m2 = this._seenSync.LoadOrMigrate(this._rowBackend, FeatureSeen, data.Seen, JsonOptions);
        var m3 = this._owedSync.LoadOrMigrate(this._rowBackend, FeatureOwed, data.Owed, JsonOptions);

        foreach (var run in data.Runs.Values)
        {
            run.Entries = new Dictionary<string, TopRaceEntry>(run.Entries, StringComparer.Ordinal);
        }

        if (m1 || m2 || m3)
        {
            this._stateStore.WriteSection(SectionKey, new TopRaceStoreData { History = data.History }, JsonOptions);
        }

        return data;
    }
}
