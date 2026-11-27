// <copyright file="AccountSafetyStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.Persistence;

/// <summary>
/// Keeps the <see cref="AccountSafetyStoreData"/>; with a file, it outlasts a restart of the server. All game servers,
/// the admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class AccountSafetyStore
{
    private const string FeatureAccounts = "accountSafety.account";
    private const string SectionKey = "accountSafety";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, AccountSafetyStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<SafetyAccountRecord> _accountSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountSafetyStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public AccountSafetyStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this._rowBackend = HnmoPlayerStateRowStoreRegistry.GetBackend(this._stateStore?.FilePath);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public AccountSafetyStoreData Data { get; }

    /// <summary>Gets the row backend backing per-account safety state.</summary>
    public IHnmoPlayerStateRowBackend RowBackend => this._rowBackend;

    /// <summary>
    /// Gets the shared store of a configured file name (relative to the server's folder).
    /// </summary>
    /// <param name="fileName">The configured file name; empty for a store in memory.</param>
    /// <returns>The store.</returns>
    public static AccountSafetyStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new AccountSafetyStore(p.Length == 0 ? null : p));
    }

    /// <summary>Marks the data as changed.</summary>
    public void Changed()
    {
        this._accountSync.MarkFullScanPending();
        this._changed = true;
    }

    /// <summary>Marks a single account dirty so <see cref="SaveIfChanged"/> writes only that row.</summary>
    /// <param name="loginName">Account login key.</param>
    public void MarkAccountDirty(string loginName)
    {
        this._accountSync.MarkDirty(loginName);
        this._changed = true;
    }

    /// <summary>
    /// Saves the data when it changed. The caller holds <see cref="Lock"/>.
    /// </summary>
    public void SaveIfChanged()
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

        this._accountSync.SaveChanges(this._rowBackend, FeatureAccounts, this.Data.Accounts, JsonOptions, useDirtySetOnly: true);
        this._changed = false;
    }

    private AccountSafetyStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new AccountSafetyStoreData();
        }

        var data = this._stateStore.ReadSection<AccountSafetyStoreData>(SectionKey, JsonOptions) ?? new AccountSafetyStoreData();
        data.Accounts = new Dictionary<string, SafetyAccountRecord>(data.Accounts, StringComparer.Ordinal);
        if (this._accountSync.LoadOrMigrate(this._rowBackend, FeatureAccounts, data.Accounts, JsonOptions))
        {
            // Strip migrated per-account records from hnmo-state.json.
            this._stateStore.WriteSection(SectionKey, new AccountSafetyStoreData(), JsonOptions);
        }

        return data;
    }
}
