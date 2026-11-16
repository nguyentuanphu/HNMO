// <copyright file="AccountSafetyStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.AccountSafety;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

/// <summary>
/// A login of an account (successful or not).
/// </summary>
public sealed class SafetyLoginEntry
{
    /// <summary>Gets or sets when.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets the address it came from (full; the website masks it).</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Gets or sets where: "game" or "website".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the password was right.</summary>
    public bool Success { get; set; }
}

/// <summary>
/// A trade or a personal store sale of an account's character.
/// </summary>
public sealed class SafetyTradeEntry
{
    /// <summary>Gets or sets when.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets what: "trade", "store-sale" (sold in the own store) or "store-buy" (bought in another's store).</summary>
    public string Kind { get; set; } = "trade";

    /// <summary>Gets or sets the account's character.</summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>Gets or sets the other character.</summary>
    public string Partner { get; set; } = string.Empty;

    /// <summary>Gets or sets the other character's account (only shown in the admin panel).</summary>
    public string PartnerLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the items given away.</summary>
    public List<string> Gave { get; set; } = new();

    /// <summary>Gets or sets the items received.</summary>
    public List<string> Got { get; set; } = new();

    /// <summary>Gets or sets the zen given away.</summary>
    public long ZenGave { get; set; }

    /// <summary>Gets or sets the zen received.</summary>
    public long ZenGot { get; set; }
}

/// <summary>
/// A locked item of an account.
/// </summary>
public sealed class SafetyItemLock
{
    /// <summary>Gets or sets the item's id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the item's text when it was locked (e.g. "Kris +7 +Luck").</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the character which had it when it was locked (empty for the vault).</summary>
    public string Character { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was locked.</summary>
    public DateTime LockedAtUtc { get; set; }

    /// <summary>Gets or sets when an asked unlock happens; <c>null</c> while no unlock was asked.</summary>
    public DateTime? UnlockAtUtc { get; set; }
}

/// <summary>
/// The safety state of an account.
/// </summary>
public sealed class SafetyAccountRecord
{
    /// <summary>Gets or sets the logins, newest first.</summary>
    public List<SafetyLoginEntry> Logins { get; set; } = new();

    /// <summary>Gets or sets the trades, newest first.</summary>
    public List<SafetyTradeEntry> Trades { get; set; } = new();

    /// <summary>Gets or sets the locked items.</summary>
    public List<SafetyItemLock> Locks { get; set; } = new();

    /// <summary>Gets or sets the wrong second passwords since the last right one or lockout.</summary>
    public int WrongTries { get; set; }

    /// <summary>Gets or sets until when the second password can't be entered.</summary>
    public DateTime? LockedOutUntilUtc { get; set; }

    /// <summary>Gets or sets when the second password was last set, changed or cleared.</summary>
    public DateTime? PinChangedAtUtc { get; set; }
}

/// <summary>
/// The saved state of the account safety, in a JSON file in the save folder (no database table).
/// </summary>
public sealed class AccountSafetyStoreData
{
    /// <summary>Gets or sets the accounts, by login name in lower case.</summary>
    public Dictionary<string, SafetyAccountRecord> Accounts { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Keeps the <see cref="AccountSafetyStoreData"/>; with a file, it outlasts a restart of the server. All game servers,
/// the admin panel and the website of a process share one store per file (<see cref="Open"/>).
/// </summary>
public sealed class AccountSafetyStore
{
    private const string SectionKey = "accountSafety";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, AccountSafetyStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountSafetyStore"/> class.
    /// </summary>
    /// <param name="filePath">The file; <c>null</c> keeps the data in memory only.</param>
    public AccountSafetyStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock which every access holds.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the data.</summary>
    public AccountSafetyStoreData Data { get; }

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

        try
        {
            this._stateStore.WriteSection(SectionKey, this.Data, JsonOptions);
            this._changed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // It's saved with the next change.
        }
    }

    private AccountSafetyStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new AccountSafetyStoreData();
        }

        var data = this._stateStore.ReadSection<AccountSafetyStoreData>(SectionKey, JsonOptions) ?? new AccountSafetyStoreData();
        data.Accounts = new Dictionary<string, SafetyAccountRecord>(data.Accounts, StringComparer.Ordinal);
        return data;
    }
}
