// <copyright file="CombiningLeftoversStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CombiningLeftovers;

using System.IO;
using System.Text.Json;
using HNMO.GameLogic.PlugIns.GiftCodes;

/// <summary>Audit entry for leftover combines and exchanges.</summary>
public sealed class LeftoversLogEntry
{
    /// <summary>Gets or sets UTC timestamp.</summary>
    public DateTime AtUtc { get; set; }

    /// <summary>Gets or sets actor login.</summary>
    public string LoginName { get; set; } = string.Empty;

    /// <summary>Gets or sets action (<c>Combine</c>, <c>CombineFail</c>, <c>Exchange</c>).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets recipe or offer identifier.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Gets or sets display summary.</summary>
    public string Detail { get; set; } = string.Empty;
}

/// <summary>Persistent state stored under section <c>combiningLeftovers</c> in <see cref="HnmoStateStore"/>.</summary>
public sealed class CombiningLeftoversStoreData
{
    /// <summary>Gets or sets newest-first log entries.</summary>
    public List<LeftoversLogEntry> Log { get; set; } = [];

    /// <summary>Gets or sets pending item/Zen rewards owed when a character's bag was full, keyed by character ID.</summary>
    public Dictionary<string, List<GiftCodeOwedReward>> Owed { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Thread-safe store over <see cref="HnmoStateStore"/> for Combining Leftovers audit logs and Season Plan lookup.
/// </summary>
public sealed class CombiningLeftoversStore
{
    private const string SectionKey = "combiningLeftovers";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly HnmoStateStore _store;

    private CombiningLeftoversStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens store for a state file path.</summary>
    /// <param name="filePath">Path or null for temp store.</param>
    /// <returns>Store instance.</returns>
    public static CombiningLeftoversStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-leftovers-{Guid.NewGuid():N}.json"));
        return new CombiningLeftoversStore(store);
    }

    /// <summary>Reads current log entries.</summary>
    /// <param name="max">Max count.</param>
    /// <returns>Entries.</returns>
    public IReadOnlyList<LeftoversLogEntry> RecentLogs(int max = 100)
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<CombiningLeftoversStoreData>(SectionKey, JsonOptions) ?? new CombiningLeftoversStoreData();
            return data.Log.Take(max).ToList();
        }
    }

    /// <summary>Records an action atomically under lock.</summary>
    /// <param name="entry">Log entry.</param>
    public void AppendLog(LeftoversLogEntry entry)
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<CombiningLeftoversStoreData>(SectionKey, JsonOptions) ?? new CombiningLeftoversStoreData();
            data.Log.Insert(0, entry);
            if (data.Log.Count > 300)
            {
                data.Log.RemoveRange(300, data.Log.Count - 300);
            }

            this._store.WriteSection(SectionKey, data, JsonOptions);
        }
    }

    /// <summary>Appends a reward owed to <paramref name="characterId"/> when bag space was full.</summary>
    /// <param name="characterId">Character ID string.</param>
    /// <param name="reward">Owed reward.</param>
    public void AddOwed(string characterId, GiftCodeOwedReward reward)
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<CombiningLeftoversStoreData>(SectionKey, JsonOptions) ?? new CombiningLeftoversStoreData();
            if (!data.Owed.TryGetValue(characterId, out var list))
            {
                list = [];
                data.Owed[characterId] = list;
            }

            list.Add(reward);
            this._store.WriteSection(SectionKey, data, JsonOptions);
        }
    }

    /// <summary>Takes all pending owed rewards for <paramref name="characterId"/>.</summary>
    /// <param name="characterId">Character ID string.</param>
    /// <returns>Taken rewards.</returns>
    public List<GiftCodeOwedReward> TakeOwed(string characterId)
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<CombiningLeftoversStoreData>(SectionKey, JsonOptions) ?? new CombiningLeftoversStoreData();
            if (!data.Owed.Remove(characterId, out var list) || list.Count == 0)
            {
                return [];
            }

            this._store.WriteSection(SectionKey, data, JsonOptions);
            return list;
        }
    }

    /// <summary>Returns undeliverable owed rewards back into storage.</summary>
    /// <param name="characterId">Character ID string.</param>
    /// <param name="rewards">Rewards that still could not fit.</param>
    public void ReturnOwed(string characterId, IReadOnlyList<GiftCodeOwedReward> rewards)
    {
        if (rewards.Count == 0)
        {
            return;
        }

        lock (this._gate)
        {
            var data = this._store.ReadSection<CombiningLeftoversStoreData>(SectionKey, JsonOptions) ?? new CombiningLeftoversStoreData();
            if (!data.Owed.TryGetValue(characterId, out var list))
            {
                list = [];
                data.Owed[characterId] = list;
            }

            list.InsertRange(0, rewards);
            this._store.WriteSection(SectionKey, data, JsonOptions);
        }
    }

    /// <summary>
    /// Reads live unlocked Season Stage <c>MaxBoxLevel</c> if <c>seasonPlan</c> state is present in the same
    /// <see cref="HnmoStateStore"/> file (loose coupling with Feature 12).
    /// </summary>
    /// <returns>Active max box level from Season Plan, or <c>null</c> if not active/present.</returns>
    public int? TryReadSeasonStageMaxBoxLevel()
    {
        lock (this._gate)
        {
            var raw = this._store.ReadSection<JsonElement>("seasonPlan", JsonOptions);
            if (raw.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Look for "MaxBoxLevel" if stored, or "UnlockedStages" count mapped gently when stages aren't duplicated here.
            if (raw.TryGetProperty("MaxBoxLevel", out var prop) && prop.TryGetInt32(out var lvl) && lvl > 0)
            {
                return lvl;
            }

            return null;
        }
    }
}
