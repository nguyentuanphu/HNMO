// <copyright file="GuildStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Guilds;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// Keeps the <see cref="GuildStoreData"/>; with a file, it outlasts a restart of the server. All game servers, the
/// admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class GuildStore
{
    private const string FeatureGuilds = "guilds.guild";
    private const string FeatureToday = "guilds.today";
    private const string FeatureOwed = "guilds.owed";
    private const string SectionKey = "guilds";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, GuildStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<GuildProgress> _guildSync = new();
    private readonly HnmoFeatureRowSync<int> _todaySync = new();
    private readonly HnmoFeatureRowSync<List<GuildOwedReward>> _owedSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public GuildStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public GuildStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static GuildStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new GuildStore(p.Length == 0 ? null : p));
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

        var guildMap = this.Data.Guilds.ToDictionary(p => p.Key.ToString(), p => p.Value, StringComparer.Ordinal);
        this._guildSync.SaveChanges(this._rowBackend, FeatureGuilds, guildMap, JsonOptions, useDirtySetOnly: true);
        this._todaySync.SaveChanges(this._rowBackend, FeatureToday, this.Data.Today, JsonOptions, useDirtySetOnly: true);
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

    private GuildStoreData BuildServerOnlyData(GuildStoreData src) => new()
    {
        DayKey = src.DayKey,
        WeekKey = src.WeekKey,
        LastWeek = src.LastWeek,
        PendingWeeks = src.PendingWeeks,
        PaidWeeks = src.PaidWeeks,
        Sieges = src.Sieges,
    };

    private GuildStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new GuildStoreData();
        }

        var data = this._stateStore.ReadSection<GuildStoreData>(SectionKey, JsonOptions) ?? new GuildStoreData();
        data.Today = new Dictionary<string, int>(data.Today, StringComparer.Ordinal);
        data.Owed = new Dictionary<string, List<GuildOwedReward>>(data.Owed, StringComparer.Ordinal);

        var guildStrMap = data.Guilds.ToDictionary(p => p.Key.ToString(), p => p.Value, StringComparer.Ordinal);
        var m1 = this._guildSync.LoadOrMigrate(this._rowBackend, FeatureGuilds, guildStrMap, JsonOptions);
        var m2 = this._todaySync.LoadOrMigrate(this._rowBackend, FeatureToday, data.Today, JsonOptions);
        var m3 = this._owedSync.LoadOrMigrate(this._rowBackend, FeatureOwed, data.Owed, JsonOptions);

        data.Guilds = guildStrMap
            .Select(kv => (Ok: Guid.TryParse(kv.Key, out var g), Id: g, kv.Value))
            .Where(x => x.Ok)
            .ToDictionary(x => x.Id, x => x.Value);
        foreach (var guild in data.Guilds.Values)
        {
            guild.WeekMembers = new Dictionary<string, long>(guild.WeekMembers, StringComparer.Ordinal);
        }

        if (m1 || m2 || m3)
        {
            this._stateStore.WriteSection(SectionKey, this.BuildServerOnlyData(data), JsonOptions);
        }

        return data;
    }
}
