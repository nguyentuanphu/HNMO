// <copyright file="MissionStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.Missions;

using System.IO;
using System.Text.Json;
using HNMO.DataModel.Configuration;
using HNMO.Persistence;

/// <summary>
/// Keeps the <see cref="MissionStoreData"/>; with a file, it outlasts a restart of the server.
/// Changes are saved by <see cref="SaveIfChanged"/> (every minute and after claims), so a crash loses at most
/// a minute of counting.
/// </summary>
public sealed class MissionStore
{
    private const string FeatureCharacters = "missions.char";
    private const string FeatureAccounts = "missions.account";
    private const string SectionKey = "missions";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore? _stateStore;
    private readonly IHnmoPlayerStateRowBackend _rowBackend;
    private readonly HnmoFeatureRowSync<MissionCharacterState> _charSync = new();
    private readonly HnmoFeatureRowSync<MissionAccountState> _accountSync = new();
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public MissionStore(string? filePath = null)
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
    public MissionStoreData Data { get; }

    /// <summary>Gets the row backend backing per-account/per-character mission state.</summary>
    public IHnmoPlayerStateRowBackend RowBackend => this._rowBackend;

    /// <summary>
    /// Gets the state of a character, created when it's new.
    /// </summary>
    /// <param name="characterId">The character id.</param>
    /// <returns>The state.</returns>
    public MissionCharacterState Character(string characterId)
    {
        if (!this.Data.Characters.TryGetValue(characterId, out var state))
        {
            state = new MissionCharacterState();
            this.Data.Characters[characterId] = state;
            this._charSync.MarkDirty(characterId);
            this._changed = true;
        }

        return state;
    }

    /// <summary>
    /// Gets the state of an account, created when it's new.
    /// </summary>
    /// <param name="loginName">The login name.</param>
    /// <returns>The state.</returns>
    public MissionAccountState Account(string loginName)
    {
        if (!this.Data.Accounts.TryGetValue(loginName, out var state))
        {
            state = new MissionAccountState();
            this.Data.Accounts[loginName] = state;
            this._accountSync.MarkDirty(loginName);
            this._changed = true;
        }

        return state;
    }

    /// <summary>
    /// Finds the account with an invite code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The login name, or <c>null</c>.</returns>
    public string? FindByInviteCode(string code) =>
        this.Data.Accounts.FirstOrDefault(a => string.Equals(a.Value.InviteCode, code, StringComparison.OrdinalIgnoreCase)).Key;

    /// <summary>Marks the data as changed.</summary>
    public void Changed()
    {
        this._charSync.MarkFullScanPending();
        this._accountSync.MarkFullScanPending();
        this._changed = true;
    }

    /// <summary>Marks one account dirty for O(1) individual row persistence.</summary>
    /// <param name="loginName">Account login key.</param>
    public void MarkAccountDirty(string loginName)
    {
        this._accountSync.MarkDirty(loginName);
        this._changed = true;
    }

    /// <summary>Marks one character dirty for O(1) individual row persistence.</summary>
    /// <param name="characterId">Character ID key.</param>
    public void MarkCharacterDirty(string characterId)
    {
        this._charSync.MarkDirty(characterId);
        this._changed = true;
    }

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
        this._accountSync.SaveChanges(this._rowBackend, FeatureAccounts, this.Data.Accounts, JsonOptions, useDirtySetOnly: true);
        this._changed = false;
    }

    private MissionStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new MissionStoreData();
        }

        var data = this._stateStore.ReadSection<MissionStoreData>(SectionKey, JsonOptions) ?? new MissionStoreData();
        data.Characters = new Dictionary<string, MissionCharacterState>(data.Characters, StringComparer.Ordinal);
        data.Accounts = new Dictionary<string, MissionAccountState>(data.Accounts, StringComparer.OrdinalIgnoreCase);
        var migratedChars = this._charSync.LoadOrMigrate(this._rowBackend, FeatureCharacters, data.Characters, JsonOptions);
        var migratedAccs = this._accountSync.LoadOrMigrate(this._rowBackend, FeatureAccounts, data.Accounts, JsonOptions);
        if (migratedChars || migratedAccs)
        {
            this._stateStore.RemoveSection(SectionKey);
        }

        return data;
    }
}
