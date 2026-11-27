// <copyright file="TalismanStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Talismans;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// Thread-safe store persisting per-character stat consumable counts via <see cref="HnmoPlayerStateRowStoreRegistry"/> and <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class TalismanStore
{
    /// <summary>Section key inside <see cref="HnmoStateStore"/> for legacy migration.</summary>
    public const string SectionKey = "talismans";

    /// <summary>Row feature key inside <see cref="IHnmoPlayerStateRowBackend"/>.</summary>
    public const string FeatureCharacters = "talismans.character";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, TalismanStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<TalismanCharacterRecord> _charSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TalismanStore"/> class.
    /// </summary>
    /// <param name="filePath">Optional state file key or path; null uses in-memory only.</param>
    public TalismanStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock guarding all reads and writes.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the in-memory store snapshot.</summary>
    public TalismanStoreData Data { get; }

    /// <summary>Gets the shared store for a configured file name.</summary>
    /// <param name="fileName">The configured file key or path.</param>
    /// <returns>The store instance.</returns>
    public static TalismanStore Open(string? fileName)
    {
        var key = string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName.Trim();
        var resolved = key.Length == 0 ? string.Empty : (HnmoStateStore.ResolveStateFilePath(key) ?? Path.GetFullPath(key, AppContext.BaseDirectory));
        return Shared.GetOrAdd(resolved, p => new TalismanStore(p.Length == 0 ? null : p));
    }

    /// <summary>Reads total extra stat points claimed so far for <paramref name="characterId"/>.</summary>
    /// <param name="characterId">Character ID string.</param>
    /// <returns>Total claimed points.</returns>
    public int GetTotalClaimed(string characterId)
    {
        lock (this.Lock)
        {
            return this.Data.Characters.TryGetValue(characterId, out var rec) ? Math.Max(0, rec.TotalExtraStatPoints) : 0;
        }
    }

    /// <summary>Records <paramref name="points"/> granted for <paramref name="characterId"/> into <paramref name="bucket"/>.</summary>
    /// <param name="characterId">Character ID string.</param>
    /// <param name="bucket">Target stat bucket.</param>
    /// <param name="points">Points granted.</param>
    /// <returns>Updated total claimed points.</returns>
    public int RecordGranted(string characterId, string bucket, int points)
    {
        if (points <= 0)
        {
            return this.GetTotalClaimed(characterId);
        }

        lock (this.Lock)
        {
            if (!this.Data.Characters.TryGetValue(characterId, out var rec))
            {
                rec = new TalismanCharacterRecord();
                this.Data.Characters[characterId] = rec;
            }

            rec.TotalExtraStatPoints += points;
            rec.ByBucket[bucket] = (rec.ByBucket.TryGetValue(bucket, out var cur) ? cur : 0) + points;
            rec.UpdatedAtUtc = DateTime.UtcNow;

            this._charSync.MarkDirty(characterId);
            this._changed = true;
            this.SaveIfChanged();
            return rec.TotalExtraStatPoints;
        }
    }

    private void SaveIfChanged()
    {
        if (!this._changed)
        {
            return;
        }

        if (this._stateStore is null)
        {
            this._changed = false;
            return;
        }

        this._charSync.SaveChanges(this._rowBackend, FeatureCharacters, this.Data.Characters, JsonOptions, useDirtySetOnly: true);
        this._changed = false;
    }

    private TalismanStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new TalismanStoreData();
        }

        var data = this._stateStore.ReadSection<TalismanStoreData>(SectionKey, JsonOptions) ?? new TalismanStoreData();
        data.Characters = new Dictionary<string, TalismanCharacterRecord>(data.Characters, StringComparer.Ordinal);
        if (this._charSync.LoadOrMigrate(this._rowBackend, FeatureCharacters, data.Characters, JsonOptions))
        {
            this._stateStore.WriteSection(SectionKey, new TalismanStoreData(), JsonOptions);
        }

        return data;
    }
}
