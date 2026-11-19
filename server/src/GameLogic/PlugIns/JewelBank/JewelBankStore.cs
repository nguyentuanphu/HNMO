// <copyright file="JewelBankStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.JewelBank;

using System.IO;
using System.Text.Json;

/// <summary>
/// A queued website jewel withdrawal awaiting claim in-game if an account's offline bag had insufficient room or was online.
/// </summary>
public sealed class JewelBankPendingWithdrawal
{
    /// <summary>Gets or sets the unique delivery identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the recipient login.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets the jewel key.</summary>
    public string JewelKey { get; set; } = string.Empty;

    /// <summary>Gets or sets remaining quantity to claim in-game.</summary>
    public int Quantity { get; set; }

    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Recent Jewel Bank activity record.
/// </summary>
public sealed class JewelBankLogEntry
{
    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets actor login.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets action (<c>Deposit</c>, <c>Withdraw</c>, <c>ClearMaster</c>).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets jewel key or detail.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets quantity.</summary>
    public int Amount { get; set; }

    /// <summary>Gets or sets origin (<c>game</c> or <c>website</c>).</summary>
    public string Source { get; set; } = "game";
}

/// <summary>
/// Serializable state stored under section <c>jewelBank</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class JewelBankStoreData
{
    /// <summary>Gets or sets account balances keyed by login name -> jewel key -> count.</summary>
    public Dictionary<string, Dictionary<string, int>> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets pending website withdrawals.</summary>
    public List<JewelBankPendingWithdrawal> PendingWithdrawals { get; set; } = [];

    /// <summary>Gets or sets newest-first log entries.</summary>
    public List<JewelBankLogEntry> Log { get; set; } = [];
}

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> for Jewel Bank balances and withdrawals.
/// </summary>
public sealed class JewelBankStore
{
    private const string SectionKey = "jewelBank";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly HnmoStateStore _store;

    private JewelBankStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the Jewel Bank store for a file path.</summary>
    /// <param name="filePath">State file path, or null for isolated temp storage.</param>
    /// <returns>Store instance.</returns>
    public static JewelBankStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-jewelbank-{Guid.NewGuid():N}.json"));
        return new JewelBankStore(store);
    }

    /// <summary>Projects data atomically under lock.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="reader">Projection function.</param>
    /// <returns>Result.</returns>
    public TResult Read<TResult>(Func<JewelBankStoreData, TResult> reader)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<JewelBankStoreData>(SectionKey, JsonOptions) ?? new JewelBankStoreData();
            return reader(data);
        }
    }

    /// <summary>Mutates data atomically under lock and flushes when changed.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="mutator">Atomic mutator.</param>
    /// <returns>Result.</returns>
    public TResult Mutate<TResult>(Func<JewelBankStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._store.SyncRoot)
        {
            var data = this._store.ReadSection<JewelBankStoreData>(SectionKey, JsonOptions) ?? new JewelBankStoreData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
