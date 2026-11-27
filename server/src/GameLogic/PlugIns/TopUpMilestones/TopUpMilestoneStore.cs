// <copyright file="TopUpMilestoneStore.cs" company="HNMO">
// Proprietary and confidential. All rights reserved. Unauthorized copying, modification, or distribution is prohibited.
// </copyright>

namespace HNMO.GameLogic.PlugIns.TopUpMilestones;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>
/// Persists account top-up totals, claimed milestones, and owed bag items via <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class TopUpMilestoneStore
{
    private const string SectionKey = "topUpMilestones";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly ConcurrentDictionary<string, TopUpMilestoneStore> Shared = new(StringComparer.Ordinal);

    private readonly HnmoStateStore? _stateStore;
    private bool _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopUpMilestoneStore"/> class.
    /// </summary>
    /// <param name="filePath">The file path, or <c>null</c> for memory-only storage.</param>
    public TopUpMilestoneStore(string? filePath = null)
    {
        this._stateStore = HnmoStateStore.Open(filePath, SectionKey);
        this.Data = this.Load();
    }

    /// <summary>Gets the lock held across reads and writes.</summary>
    public object Lock { get; } = new();

    /// <summary>Gets the persisted store data.</summary>
    public TopUpMilestoneStoreData Data { get; }

    /// <summary>
    /// Opens or returns the process-wide shared store for <paramref name="fileName"/>.
    /// </summary>
    /// <param name="fileName">The configured file name.</param>
    /// <returns>The store instance.</returns>
    public static TopUpMilestoneStore Open(string? fileName)
    {
        var path = string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetFullPath(fileName.Trim(), AppContext.BaseDirectory);
        return Shared.GetOrAdd(path, p => new TopUpMilestoneStore(p.Length == 0 ? null : p));
    }

    /// <summary>Marks the store as dirty.</summary>
    public void Changed() => this._changed = true;

    /// <summary>Persists changes if dirty. Caller holds <see cref="Lock"/>.</summary>
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
            // Retained in memory until next save.
        }
    }

    private TopUpMilestoneStoreData Load()
    {
        if (this._stateStore is null)
        {
            return new TopUpMilestoneStoreData();
        }

        var data = this._stateStore.ReadSection<TopUpMilestoneStoreData>(SectionKey, JsonOptions) ?? new TopUpMilestoneStoreData();
        var normalized = new Dictionary<string, TopUpAccountState>(StringComparer.Ordinal);
        foreach (var (key, state) in data.Accounts)
        {
            state.CreditedReferences = new HashSet<string>(state.CreditedReferences, StringComparer.OrdinalIgnoreCase);
            state.ClaimedStepIds = new HashSet<string>(state.ClaimedStepIds, StringComparer.OrdinalIgnoreCase);
            normalized[key.ToLowerInvariant()] = state;
        }

        data.Accounts = normalized;
        return data;
    }
}
