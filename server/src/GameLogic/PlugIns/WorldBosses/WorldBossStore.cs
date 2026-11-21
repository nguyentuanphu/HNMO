// <copyright file="WorldBossStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.WorldBosses;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// Keeps the <see cref="WorldBossStoreData"/>; with a file, it outlasts a restart of the server. All game servers and
/// the admin panel of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class WorldBossStore
{
    private const string FeatureOwed = "worldBosses.owed";
    private const string SectionKey = "worldBosses";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, WorldBossStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<List<WorldBossOwedReward>> _owedSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldBossStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public WorldBossStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the file, if any.</summary>
    public string? FilePath => this._stateStore?.FilePath;

    /// <summary>Gets the data.</summary>
    public WorldBossStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static WorldBossStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new WorldBossStore(p.Length == 0 ? null : p));
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

        this._owedSync.SaveChanges(this._rowBackend, FeatureOwed, this.Data.Owed, JsonOptions, useDirtySetOnly: true);

        try
        {
            this._stateStore.WriteSection(SectionKey, this.BuildServerOnlyData(this.Data), JsonOptions);
            this._changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private WorldBossStoreData BuildServerOnlyData(WorldBossStoreData src) => new()
    {
        Bosses = src.Bosses,
        Alive = src.Alive,
        Kills = src.Kills,
        Announcements = src.Announcements,
        WeekKey = src.WeekKey,
        Week = src.Week,
        CastleSiegeGuildId = src.CastleSiegeGuildId,
        CastleSiegeGuildName = src.CastleSiegeGuildName,
        LastWeek = src.LastWeek,
        PendingWeeks = src.PendingWeeks,
        PaidWeeks = src.PaidWeeks,
    };

    private WorldBossStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new WorldBossStoreData();
        }

        var data = this._stateStore.ReadSection<WorldBossStoreData>(SectionKey, JsonOptions) ?? new WorldBossStoreData();
        data.Bosses = new Dictionary<string, WorldBossState>(data.Bosses, StringComparer.Ordinal);
        data.Owed = new Dictionary<string, List<WorldBossOwedReward>>(data.Owed, StringComparer.Ordinal);
        foreach (var alive in data.Alive)
        {
            alive.Damage = new Dictionary<string, WorldBossDamageEntry>(alive.Damage, StringComparer.Ordinal);
        }

        if (this._owedSync.LoadOrMigrate(this._rowBackend, FeatureOwed, data.Owed, JsonOptions))
        {
            this._stateStore.WriteSection(SectionKey, this.BuildServerOnlyData(data), JsonOptions);
        }

        return data;
    }
}
