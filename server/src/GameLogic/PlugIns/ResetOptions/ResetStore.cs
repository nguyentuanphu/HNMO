// <copyright file="ResetStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.ResetOptions;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// Keeps the <see cref="ResetStoreData"/>; with a file, it outlasts a restart of the server. All game servers, the
/// admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class ResetStore
{
    private const string FeatureCharacters = "resets.char";
    private const string SectionKey = "resets";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, ResetStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<ResetCharacterRecord> _charSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public ResetStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public ResetStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static ResetStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new ResetStore(p.Length == 0 ? null : p));
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

        this._charSync.SaveChanges(this._rowBackend, FeatureCharacters, this.Data.Characters, JsonOptions, useDirtySetOnly: true);
        this.WriteServerSection();
        this._changed = false;
    }

    private void WriteServerSection()
    {
        if (this._stateStore is null)
        {
            return;
        }

        try
        {
            this._stateStore.WriteSection(
                SectionKey,
                new ResetStoreData { Months = this.Data.Months, Log = this.Data.Log },
                JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private ResetStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new ResetStoreData();
        }

        var data = this._stateStore.ReadSection<ResetStoreData>(SectionKey, JsonOptions) ?? new ResetStoreData();
        data.Characters = new Dictionary<string, ResetCharacterRecord>(data.Characters, StringComparer.Ordinal);
        data.Months = data.Months.ToDictionary(p => p.Key, p => new Dictionary<string, int>(p.Value, StringComparer.Ordinal), StringComparer.Ordinal);
        if (this._charSync.LoadOrMigrate(this._rowBackend, FeatureCharacters, data.Characters, JsonOptions))
        {
            this._stateStore.WriteSection(SectionKey, new ResetStoreData { Months = data.Months, Log = data.Log }, JsonOptions);
        }

        return data;
    }
}
