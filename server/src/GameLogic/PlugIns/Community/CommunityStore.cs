// <copyright file="CommunityStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Community;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.Missions;
using HNMO.Persistence;

/// <summary>
/// The state of a Zalo claim.
/// </summary>
public enum ZaloClaimStatus
{
    /// <summary>No claim.</summary>
    None,

    /// <summary>The player asked; the admin hasn't decided yet.</summary>
    Pending,

    /// <summary>The admin approved it; the coins are paid (or paid on the next tick).</summary>
    Approved,

    /// <summary>The admin rejected it; the player can ask again.</summary>
    Rejected,
}

/// <summary>
/// Keeps the <see cref="CommunityStoreData"/>; with a file, it outlasts a restart of the server. All game servers, the
/// admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class CommunityStore
{
    private const string FeatureAccounts = "community.account";
    private const string FeatureEntries = "community.entry";
    private const string FeatureOwed = "community.owed";
    private const string SectionKey = "community";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, CommunityStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<CommunityAccountState> _accSync = new();
    private readonly HnmoFeatureRowSync<CommunityEventEntry> _entrySync = new();
    private readonly HnmoFeatureRowSync<List<CommunityOwedReward>> _owedSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommunityStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public CommunityStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public CommunityStoreData Data { get; }

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static CommunityStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new CommunityStore(p.Length == 0 ? null : p));
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

        this._accSync.SaveChanges(this._rowBackend, FeatureAccounts, this.Data.Accounts, JsonOptions, useDirtySetOnly: true);
        this._entrySync.SaveChanges(this._rowBackend, FeatureEntries, this.Data.Entries, JsonOptions, useDirtySetOnly: true);
        this._owedSync.SaveChanges(this._rowBackend, FeatureOwed, this.Data.Owed, JsonOptions, useDirtySetOnly: true);

        try
        {
            this._stateStore.WriteSection(
                SectionKey,
                new CommunityStoreData
                {
                    PaidDiscordUsers = this.Data.PaidDiscordUsers,
                    CodeAccounts = this.Data.CodeAccounts,
                    Highlights = this.Data.Highlights,
                },
                JsonOptions);
            this._changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private CommunityStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new CommunityStoreData();
        }

        var data = this._stateStore.ReadSection<CommunityStoreData>(SectionKey, JsonOptions) ?? new CommunityStoreData();
        data.Accounts = new Dictionary<string, CommunityAccountState>(data.Accounts, StringComparer.Ordinal);
        data.PaidDiscordUsers = new Dictionary<string, string>(data.PaidDiscordUsers, StringComparer.Ordinal);
        data.Entries = new Dictionary<string, CommunityEventEntry>(data.Entries, StringComparer.Ordinal);
        data.CodeAccounts = new Dictionary<string, string>(data.CodeAccounts, StringComparer.Ordinal);
        data.Owed = new Dictionary<string, List<CommunityOwedReward>>(data.Owed, StringComparer.Ordinal);

        var m1 = this._accSync.LoadOrMigrate(this._rowBackend, FeatureAccounts, data.Accounts, JsonOptions);
        var m2 = this._entrySync.LoadOrMigrate(this._rowBackend, FeatureEntries, data.Entries, JsonOptions);
        var m3 = this._owedSync.LoadOrMigrate(this._rowBackend, FeatureOwed, data.Owed, JsonOptions);
        if (m1 || m2 || m3)
        {
            this._stateStore.WriteSection(
                SectionKey,
                new CommunityStoreData
                {
                    PaidDiscordUsers = data.PaidDiscordUsers,
                    CodeAccounts = data.CodeAccounts,
                    Highlights = data.Highlights,
                },
                JsonOptions);
        }

        return data;
    }
}
