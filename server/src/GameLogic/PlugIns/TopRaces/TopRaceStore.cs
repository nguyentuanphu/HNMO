// <copyright file="TopRaceStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopRaces;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;
using HNMO.GameLogic.PlugIns.Missions;

/// <summary>
/// A character's (or a guild's) value in a running race.
/// </summary>
public sealed class TopRaceEntry
{
    /// <summary>Gets or sets the key: the character id, or "guild:" and the persistent guild id.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the character's or guild's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name (empty for a guild).</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the class (<see cref="TopRaceClass"/>); -1 for a guild.</summary>
    public int Class { get; set; } = -1;

    /// <summary>Gets or sets the value.</summary>
    public long Value { get; set; }

    /// <summary>Gets or sets when the value was last raised (an earlier time wins a tie).</summary>
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>
/// A race which runs now (one period of it).
/// </summary>
public sealed class TopRaceRun
{
    /// <summary>Gets or sets the race's id.</summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the period's key, e.g. "w2026-11-09".</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the start.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Gets or sets the end.</summary>
    public DateTime EndUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether its start was announced.</summary>
    public bool StartAnnounced { get; set; }

    /// <summary>Gets or sets the entries by key.</summary>
    public Dictionary<string, TopRaceEntry> Entries { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// A line of a finished race's table.
/// </summary>
public sealed class TopRaceLine
{
    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the entry's key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's login name (not shown).</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Gets or sets the class; -1 for a guild.</summary>
    public int Class { get; set; } = -1;

    /// <summary>Gets or sets the value.</summary>
    public long Value { get; set; }

    /// <summary>Gets or sets the coins of the prize.</summary>
    public int Coins { get; set; }

    /// <summary>Gets or sets the coin type.</summary>
    public CashShopCoinType CoinType { get; set; }

    /// <summary>Gets or sets the zen of the prize.</summary>
    public int Zen { get; set; }

    /// <summary>Gets or sets the items of the prize.</summary>
    public List<MissionItemReward> Items { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the line got a prize.</summary>
    public bool Prized { get; set; }
}

/// <summary>
/// A table of a finished race: all classes, or one class.
/// </summary>
public sealed class TopRaceTable
{
    /// <summary>Gets or sets the class of the table; -1 for all classes.</summary>
    public int Class { get; set; } = -1;

    /// <summary>Gets or sets the lines, best first.</summary>
    public List<TopRaceLine> Lines { get; set; } = new();
}

/// <summary>
/// A finished race.
/// </summary>
public sealed class TopRaceResult
{
    /// <summary>Gets or sets the race's id.</summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English title.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets what it counted.</summary>
    public TopRaceKind Kind { get; set; }

    /// <summary>Gets or sets the period.</summary>
    public TopRacePeriod Period { get; set; }

    /// <summary>Gets or sets the period's key.</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the start.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Gets or sets the end.</summary>
    public DateTime EndUtc { get; set; }

    /// <summary>Gets or sets the tables.</summary>
    public List<TopRaceTable> Tables { get; set; } = new();

    /// <summary>Gets or sets the references which were paid already (so a retry pays nobody twice).</summary>
    public List<string> PaidReferences { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether all prizes were paid.</summary>
    public bool Paid { get; set; }

    /// <summary>Gets or sets a value indicating whether its end was announced.</summary>
    public bool EndAnnounced { get; set; }
}

/// <summary>
/// A prize which waits for room in the bag (zen and items; the coins are given at once).
/// </summary>
public sealed class TopRaceOwedReward
{
    /// <summary>Gets or sets the reference; it makes the prize count once.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Gets or sets the race's id.</summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the English title.</summary>
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>Gets or sets the Vietnamese title.</summary>
    public string TitleVi { get; set; } = string.Empty;

    /// <summary>Gets or sets the period's key.</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the rank.</summary>
    public int Rank { get; set; }

    /// <summary>Gets or sets the prize (zen and items).</summary>
    public MissionReward Reward { get; set; } = new();
}

/// <summary>
/// The last level and resets seen of a character (for the levels and resets gained).
/// </summary>
public sealed class TopRaceSeen
{
    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the resets.</summary>
    public int Resets { get; set; }
}

/// <summary>
/// The saved state of the top races, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class TopRaceStoreData
{
    /// <summary>Gets or sets the running races by "race id|period key".</summary>
    public Dictionary<string, TopRaceRun> Runs { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the last level and resets seen, by character id.</summary>
    public Dictionary<string, TopRaceSeen> Seen { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the finished races, newest first.</summary>
    public List<TopRaceResult> History { get; set; } = new();

    /// <summary>Gets or sets the prizes which wait for room in the bag, by character id.</summary>
    public Dictionary<string, List<TopRaceOwedReward>> Owed { get; set; } = new(StringComparer.Ordinal);
}

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

        static DateTime? RunExpiry(string _, TopRaceRun run) =>
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

        static DateTime? RunExpiry(string _, TopRaceRun run) =>
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
