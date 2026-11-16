// <copyright file="CatchupCapStore.cs" company="HNMO">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace HNMO.GameLogic.PlugIns.CatchupCap;

using System.IO;
using System.Text.Json;

/// <summary>
/// Persistent state stored under section <c>catchupCap</c> in <see cref="HnmoStateStore"/>.
/// </summary>
public sealed class CatchupCapStoreData
{
    /// <summary>Gets or sets UTC timestamp when the first player hit the level trigger.</summary>
    public DateTime? LevelTriggeredAtUtc { get; set; }

    /// <summary>Gets or sets character name of the first player who reached the initial level cap.</summary>
    public string LevelFirstPlayer { get; set; } = string.Empty;

    /// <summary>Gets or sets day key (yyyy-MM-dd in configured timezone) when level cap triggered.</summary>
    public string LevelTriggerDayKey { get; set; } = string.Empty;

    /// <summary>Gets or sets UTC timestamp when the first player hit the reset trigger.</summary>
    public DateTime? ResetTriggeredAtUtc { get; set; }

    /// <summary>Gets or sets character name of the first player who hit the reset trigger.</summary>
    public string ResetFirstPlayer { get; set; } = string.Empty;

    /// <summary>Gets or sets day key when reset cap triggered.</summary>
    public string ResetTriggerDayKey { get; set; } = string.Empty;
}

/// <summary>
/// Thread-safe persistence wrapper over <see cref="HnmoStateStore"/> for Catch-up Level/Reset Cap state.
/// </summary>
public sealed class CatchupCapStore
{
    private const string SectionKey = "catchupCap";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly HnmoStateStore _store;

    private CatchupCapStore(HnmoStateStore store)
    {
        this._store = store;
    }

    /// <summary>Opens the Catch-up Cap store for a file path.</summary>
    /// <param name="filePath">State file path, or null for temp file.</param>
    /// <returns>Store instance.</returns>
    public static CatchupCapStore Open(string? filePath)
    {
        var store = (string.IsNullOrWhiteSpace(filePath) ? null : HnmoStateStore.Open(filePath))
            ?? new HnmoStateStore(Path.Combine(Path.GetTempPath(), $"hnmo-catchup-{Guid.NewGuid():N}.json"));
        return new CatchupCapStore(store);
    }

    /// <summary>Projects data atomically under lock.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="reader">Reader delegate.</param>
    /// <returns>Projected value.</returns>
    public TResult Read<TResult>(Func<CatchupCapStoreData, TResult> reader)
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<CatchupCapStoreData>(SectionKey, JsonOptions) ?? new CatchupCapStoreData();
            return reader(data);
        }
    }

    /// <summary>Mutates data atomically under lock and writes to disk when modified.</summary>
    /// <typeparam name="TResult">Return type.</typeparam>
    /// <param name="mutator">Mutator delegate.</param>
    /// <returns>Outcome.</returns>
    public TResult Mutate<TResult>(Func<CatchupCapStoreData, (bool Changed, TResult Result)> mutator)
    {
        lock (this._gate)
        {
            var data = this._store.ReadSection<CatchupCapStoreData>(SectionKey, JsonOptions) ?? new CatchupCapStoreData();
            var (changed, result) = mutator(data);
            if (changed)
            {
                this._store.WriteSection(SectionKey, data, JsonOptions);
            }

            return result;
        }
    }
}
